using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using Mdbase.Core.Configuration;
using Mdbase.Core.Discovery;
using Mdbase.Core.Links;
using Mdbase.Core.Loading;
using Mdbase.Core.Matching;
using Mdbase.Core.Yaml;

namespace Mdbase.Core;

/// <summary>
/// A stateful, in-memory handle to a loaded mdbase collection (spec Ch.01/02; #7 point 5).
/// <see cref="Connect"/> runs the strict two-phase load decided in #8: phase 1 (type discovery
/// + compile, including type-registry conflict detection) completes before phase 2 (record
/// scan, match, construct) starts — never interleaved.
/// </summary>
public sealed class MdbCollection
{
    private const string DefaultRuntimeExcludedName1 = ".git";
    private const string DefaultRuntimeExcludedName2 = "node_modules";

    private readonly Dictionary<string, MdbType> _typesByCanonicalName;
    private readonly Dictionary<string, MdbRecord> _recordsByPath;
    private readonly List<MdbDiagnostic> _diagnostics;
    private readonly ResolutionIndexes _linkIndexes;
    private readonly Dictionary<string, List<MdbBacklinkEntry>> _backlinksByTarget = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _outgoingTargetsBySource = new(StringComparer.Ordinal);

    private MdbCollection(
        string rootPath,
        MdbCollectionConfig config,
        Dictionary<string, MdbType> types,
        Dictionary<string, MdbRecord> records,
        List<MdbDiagnostic> diagnostics)
    {
        RootPath = rootPath;
        Config = config;
        _typesByCanonicalName = types;
        _recordsByPath = records;
        _diagnostics = diagnostics;
        _linkIndexes = new ResolutionIndexes(config.IdField);
    }

    /// <summary>Absolute path to the collection root (the directory containing the active `mdbase.yaml`).</summary>
    public string RootPath { get; }

    public MdbCollectionConfig Config { get; }

    /// <summary>The loaded type registry, keyed by canonical (lower-case) type name.</summary>
    public IReadOnlyDictionary<string, MdbType> Types => _typesByCanonicalName;

    /// <summary>Every indexed record, keyed by collection-relative forward-slash path.</summary>
    public IReadOnlyDictionary<string, MdbRecord> Records => _recordsByPath;

    /// <summary>Registry-level diagnostics: invalid/rejected type files and duplicate type-name conflicts.</summary>
    public IReadOnlyList<MdbDiagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Every resolved, non-ambiguous <see cref="MdbBacklinkEntry"/> whose link targets
    /// <paramref name="path"/> (#9 point 3 backward index). Empty for a path with no incoming
    /// resolved links.
    /// </summary>
    public IReadOnlyList<MdbBacklinkEntry> GetBacklinks(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return _backlinksByTarget.TryGetValue(normalized, out var entries) ? entries : Array.Empty<MdbBacklinkEntry>();
    }

    /// <summary>
    /// Opens an mdbase collection at <paramref name="path"/>, running the full two-phase load.
    /// </summary>
    /// <exception cref="MdbCollectionNotFoundException">No `mdbase.yaml` was found at <paramref name="path"/>.</exception>
    public static MdbCollection Connect(string path)
    {
        var root = Path.GetFullPath(path);
        var configPath = Path.Combine(root, "mdbase.yaml");
        if (!File.Exists(configPath))
        {
            throw new MdbCollectionNotFoundException(root);
        }

        MdbCollectionConfig config;
        try
        {
            config = MdbCollectionConfig.Parse(File.ReadAllText(configPath));
        }
        catch (FrontmatterParseException ex)
        {
            throw new InvalidOperationException($"mdbase.yaml at '{configPath}' is invalid: {ex.Message}", ex);
        }

        var diagnostics = new List<MdbDiagnostic>();
        var types = BuildTypeRegistry(root, config, diagnostics);

        var collection = new MdbCollection(root, config, types, new Dictionary<string, MdbRecord>(StringComparer.Ordinal), diagnostics);
        foreach (var relativePath in collection.DiscoverRecordPaths())
        {
            collection._recordsByPath[relativePath] = collection.LoadSingleRecord(relativePath);
        }

        collection.RunFullLinkPhase();

        return collection;
    }

    /// <summary>
    /// Re-derives the affected part of the in-memory index for one changed path (#8 resolution
    /// point 7): a record-path change patches just that record; a type-path change rebuilds the
    /// whole type registry and re-runs matching for every already-indexed record.
    /// </summary>
    public void Refresh(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/').TrimStart('/');

        if (IsUnderFolder(relativePath, Config.TypesFolder))
        {
            RefreshTypeRegistry();
            return;
        }

        RefreshRecord(relativePath);
    }

    private void RefreshTypeRegistry()
    {
        _typesByCanonicalName.Clear();
        var diagnostics = new List<MdbDiagnostic>();
        foreach (var (name, type) in BuildTypeRegistry(RootPath, Config, diagnostics))
        {
            _typesByCanonicalName[name] = type;
        }

        _diagnostics.Clear();
        _diagnostics.AddRange(diagnostics);

        foreach (var relativePath in _recordsByPath.Keys.ToArray())
        {
            _recordsByPath[relativePath] = LoadSingleRecord(relativePath);
        }

        // A `collection.links` rule change is exactly as blast-radius-everything as a
        // `read_defaults` change already is — rebuild both resolution dictionaries and the
        // backward index from scratch (#9's Refresh(typePath) resolution).
        RunFullLinkPhase();
    }

    /// <summary>
    /// Patches just this one record's phase-3 state (#9 point 6). Does not retroactively
    /// re-resolve any other record's previously unresolved or ambiguous links against this
    /// change — accepted staleness trade-off, see <see cref="Refresh"/>.
    /// </summary>
    private void RefreshRecord(string relativePath)
    {
        var absolutePath = Path.Combine(RootPath, relativePath);
        var hadOldRecord = _recordsByPath.TryGetValue(relativePath, out var oldRecord);

        if (!File.Exists(absolutePath) || IsReservedOrExcludedRecordPath(relativePath) || !HasRecordExtension(relativePath))
        {
            _recordsByPath.Remove(relativePath);
            if (hadOldRecord)
            {
                RemoveOutgoingBacklinks(relativePath);
                _linkIndexes.Remove(relativePath, oldRecord!);
            }

            return;
        }

        var newRecord = LoadSingleRecord(relativePath);

        if (hadOldRecord)
        {
            _linkIndexes.UpdateRecord(relativePath, oldRecord!, newRecord);
        }
        else
        {
            _linkIndexes.Add(relativePath, newRecord);
        }

        RemoveOutgoingBacklinks(relativePath);

        var result = LinkIndexer.ComputeLinks(newRecord, _linkIndexes, _recordsByPath, Config.Validation);
        _recordsByPath[relativePath] = result.Record;
        InsertOutgoingBacklinks(relativePath, result.Outgoing);
    }

    /// <summary>Full phase-3 rebuild (#9 point 1): both resolution dictionaries and the backward index, from the current, complete phase-2 record inventory.</summary>
    private void RunFullLinkPhase()
    {
        _linkIndexes.RebuildFull(_recordsByPath);
        _backlinksByTarget.Clear();
        _outgoingTargetsBySource.Clear();

        foreach (var relativePath in _recordsByPath.Keys.ToArray())
        {
            var result = LinkIndexer.ComputeLinks(_recordsByPath[relativePath], _linkIndexes, _recordsByPath, Config.Validation);
            _recordsByPath[relativePath] = result.Record;
            InsertOutgoingBacklinks(relativePath, result.Outgoing);
        }
    }

    private void RemoveOutgoingBacklinks(string sourcePath)
    {
        if (!_outgoingTargetsBySource.TryGetValue(sourcePath, out var targets))
        {
            return;
        }

        foreach (var target in targets)
        {
            if (!_backlinksByTarget.TryGetValue(target, out var entries))
            {
                continue;
            }

            entries.RemoveAll(e => string.Equals(e.SourcePath, sourcePath, StringComparison.Ordinal));
            if (entries.Count == 0)
            {
                _backlinksByTarget.Remove(target);
            }
        }

        _outgoingTargetsBySource.Remove(sourcePath);
    }

    private void InsertOutgoingBacklinks(string sourcePath, IReadOnlyList<OutgoingLink> outgoing)
    {
        foreach (var (fieldPath, link) in outgoing)
        {
            if (link.ResolvedPath is null || link.IsAmbiguous)
            {
                continue;
            }

            if (!_backlinksByTarget.TryGetValue(link.ResolvedPath, out var entries))
            {
                entries = new List<MdbBacklinkEntry>();
                _backlinksByTarget[link.ResolvedPath] = entries;
            }

            entries.Add(new MdbBacklinkEntry { SourcePath = sourcePath, FieldPath = fieldPath, Link = link });

            if (!_outgoingTargetsBySource.TryGetValue(sourcePath, out var targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                _outgoingTargetsBySource[sourcePath] = targets;
            }

            targets.Add(link.ResolvedPath);
        }
    }

    private static Dictionary<string, MdbType> BuildTypeRegistry(string root, MdbCollectionConfig config, List<MdbDiagnostic> diagnostics)
    {
        var typesRoot = Path.Combine(root, config.TypesFolder);
        var candidates = PruningWalker.WalkFiles(root, typesRoot, relativeDir => IsPrunedDirectory(relativeDir, root, config, isTypeWalk: true));

        var loaded = new List<MdbType>();
        foreach (var absolutePath in candidates)
        {
            if (!string.Equals(Path.GetExtension(absolutePath), ".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = PathUtil.ToRelative(root, absolutePath);
            OrderedDictionary frontmatter;
            try
            {
                frontmatter = FrontmatterParser.Parse(File.ReadAllText(absolutePath)).Frontmatter;
            }
            catch (FrontmatterParseException ex)
            {
                diagnostics.Add(new MdbDiagnostic
                {
                    Severity = MdbSeverity.Warning,
                    Code = "type_invalid",
                    Message = $"Could not parse frontmatter: {ex.Message}",
                    Path = relativePath,
                });
                continue;
            }

            if (!TypeFileLoader.IsTypeCandidate(frontmatter))
            {
                continue;
            }

            try
            {
                loaded.Add(TypeFileLoader.Load(frontmatter, relativePath, root));
            }
            catch (TypeFileException ex)
            {
                diagnostics.Add(new MdbDiagnostic
                {
                    Severity = MdbSeverity.Error,
                    Code = ex.Code,
                    Message = ex.Message,
                    Path = relativePath,
                });
            }
        }

        var registry = new Dictionary<string, MdbType>(StringComparer.Ordinal);
        foreach (var group in loaded.GroupBy(t => t.CanonicalName))
        {
            var definitions = group.ToArray();
            if (definitions.Length == 1)
            {
                registry[group.Key] = definitions[0];
                continue;
            }

            diagnostics.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "type_conflict",
                Message = $"Type name '{group.Key}' is defined by {definitions.Length} type files; type names are compared case-insensitively.",
                Details = new Dictionary<string, object?> { ["files"] = definitions.Select(t => t.FilePath).ToArray() },
            });
        }

        return registry;
    }

    private IEnumerable<string> DiscoverRecordPaths()
    {
        var candidates = PruningWalker.WalkFiles(RootPath, RootPath, relativeDir => IsPrunedDirectory(relativeDir, RootPath, Config, isTypeWalk: false));
        foreach (var absolutePath in candidates)
        {
            var relativePath = PathUtil.ToRelative(RootPath, absolutePath);
            if (HasRecordExtension(relativePath) && !IsReservedOrExcludedRecordPath(relativePath))
            {
                yield return relativePath;
            }
        }
    }

    private bool HasRecordExtension(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).TrimStart('.');
        return Config.RecordExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsReservedOrExcludedRecordPath(string relativePath) =>
        IsUnderFolder(relativePath, Config.TypesFolder)
        || IsUnderFolder(relativePath, Config.ContractsFolder)
        || IsUnderFolder(relativePath, ".mdbase")
        || string.Equals(relativePath, "mdbase.yaml", StringComparison.Ordinal)
        || Config.Exclude.Any(glob => GlobPattern.Compile(glob).IsMatch(relativePath));

    private static bool IsPrunedDirectory(string relativeDir, string root, MdbCollectionConfig config, bool isTypeWalk)
    {
        if (relativeDir.Length == 0)
        {
            return false;
        }


        if (!config.IncludeSubfolders && !isTypeWalk)
        {
            return true;
        }

        var name = relativeDir[(relativeDir.LastIndexOf('/') + 1)..];
        if (name is DefaultRuntimeExcludedName1 or DefaultRuntimeExcludedName2)
        {
            return true;
        }

        if (relativeDir == ".mdbase" || relativeDir.StartsWith(".mdbase/", StringComparison.Ordinal))
        {
            return true;
        }

        if (!isTypeWalk)
        {
            if (IsUnderFolder(relativeDir, config.TypesFolder) || relativeDir == config.TypesFolder)
            {
                return true;
            }

            if (IsUnderFolder(relativeDir, config.ContractsFolder) || relativeDir == config.ContractsFolder)
            {
                return true;
            }
        }

        if (config.Exclude.Any(glob => GlobPattern.Compile(glob).IsMatch(relativeDir)))
        {
            return true;
        }

        return File.Exists(Path.Combine(root, relativeDir, "mdbase.yaml"));
    }

    private static bool IsUnderFolder(string relativePath, string folder) =>
        folder.Length > 0 && (relativePath == folder || relativePath.StartsWith(folder + "/", StringComparison.Ordinal));

    private MdbRecord LoadSingleRecord(string relativePath)
    {
        var absolutePath = Path.Combine(RootPath, relativePath);
        var bytes = File.ReadAllBytes(absolutePath);
        var revision = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(StripUtf8Bom(bytes));

        var extraDiagnostics = new List<MdbDiagnostic>();
        OrderedDictionary frontmatter;
        string body;
        try
        {
            var parsed = FrontmatterParser.Parse(text);
            frontmatter = parsed.Frontmatter;
            body = parsed.Body;
        }
        catch (FrontmatterParseException ex)
        {
            frontmatter = new OrderedDictionary();
            body = text;
            extraDiagnostics.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "frontmatter_invalid",
                Message = ex.Message,
                Path = relativePath,
            });
        }

        var matchedTypes = DetermineMatchedTypes(relativePath, frontmatter, extraDiagnostics);
        return RecordLoader.Load(relativePath, frontmatter, body, revision, matchedTypes, extraDiagnostics);
    }

    private static byte[] StripUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? bytes[3..] : bytes;

    /// <summary>The matching decision process (spec Ch.07 "Matching Decision Process").</summary>
    private IReadOnlyList<MdbType> DetermineMatchedTypes(
        string relativePath, OrderedDictionary frontmatter, List<MdbDiagnostic> diagnostics)
    {
        var presentKeys = Config.ExplicitTypeKeys.Where(frontmatter.Contains).ToArray();
        if (presentKeys.Length > 0)
        {
            var names = new List<string>();
            foreach (var key in presentKeys)
            {
                var value = frontmatter[key];
                var declared = value switch
                {
                    string s => new[] { s },
                    object?[] arr when arr.Length > 0 && arr.All(i => i is string) => arr.Select(i => (string)i!).ToArray(),
                    _ => null,
                };

                if (declared is null)
                {
                    diagnostics.Add(new MdbDiagnostic
                    {
                        Severity = MdbSeverity.Error,
                        Code = "type_invalid",
                        Message = $"Explicit type key '{key}' must be a type-name string or a non-empty list of type-name strings.",
                        Path = relativePath,
                        Field = key,
                    });
                    continue;
                }

                names.AddRange(declared);
            }

            var deduped = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (seen.Add(name))
                {
                    deduped.Add(name);
                }
            }

            var resolved = new List<MdbType>();
            foreach (var name in deduped)
            {
                if (_typesByCanonicalName.TryGetValue(name.ToLowerInvariant(), out var type))
                {
                    resolved.Add(type);
                }
                else
                {
                    diagnostics.Add(new MdbDiagnostic
                    {
                        Severity = MdbSeverity.Error,
                        Code = "type_invalid",
                        Message = $"Explicit type declaration '{name}' does not match any type in the registry.",
                        Path = relativePath,
                    });
                }
            }

            return resolved;
        }

        return _typesByCanonicalName.Values
            .Where(t => t.Match.Matches(relativePath, frontmatter))
            .OrderBy(t => t.CanonicalName, StringComparer.Ordinal)
            .ToList();
    }
}
