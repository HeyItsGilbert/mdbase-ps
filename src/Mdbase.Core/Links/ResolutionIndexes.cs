namespace Mdbase.Core.Links;

/// <summary>
/// The two auxiliary phase-3 resolution structures (#9 point 4), plus the full path universe
/// path-style resolution needs. Owned and mutated by <see cref="MdbCollection"/> across
/// <c>Connect</c>/<c>Refresh</c> — <see cref="Add"/>/<see cref="Remove"/>/<see cref="UpdateRecord"/>
/// keep it incrementally correct for a single-record refresh (#9 point 6) without a full
/// collection rebuild; <see cref="RebuildFull"/> backs the full-rebuild paths (initial
/// <c>Connect</c>, <c>Refresh(typePath)</c>).
///
/// Both the `id_field` and basename indexes track every current owner path per key (not just a
/// first writer) so that removing one of several same-valued records correctly un-ambiguates the
/// remainder, and so a later same-valued record correctly re-ambiguates it.
/// </summary>
internal sealed class ResolutionIndexes
{
    private readonly string _idField;
    private readonly HashSet<string> _allPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _idOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _basenameOwners = new(StringComparer.Ordinal);

    public ResolutionIndexes(string idField)
    {
        _idField = idField;
    }

    public IReadOnlySet<string> AllPaths => _allPaths;

    public static ResolutionIndexes BuildFull(IReadOnlyDictionary<string, MdbRecord> records, string idField)
    {
        var indexes = new ResolutionIndexes(idField);
        indexes.RebuildFull(records);
        return indexes;
    }

    public void RebuildFull(IReadOnlyDictionary<string, MdbRecord> records)
    {
        _allPaths.Clear();
        _idOwners.Clear();
        _basenameOwners.Clear();

        foreach (var (path, record) in records)
        {
            Add(path, record);
        }
    }

    public void Add(string path, MdbRecord record)
    {
        _allPaths.Add(path);

        var idValue = GetIdValue(record);
        if (idValue is not null)
        {
            AddOwner(_idOwners, idValue, path);
        }

        AddOwner(_basenameOwners, GetBasename(path), path);
    }

    public void Remove(string path, MdbRecord record)
    {
        _allPaths.Remove(path);

        var idValue = GetIdValue(record);
        if (idValue is not null)
        {
            RemoveOwner(_idOwners, idValue, path);
        }

        RemoveOwner(_basenameOwners, GetBasename(path), path);
    }

    /// <summary>Patches the id-value ownership for a record whose path is unchanged (#9 point 6: "only if that record's own ID... changed").</summary>
    public void UpdateRecord(string path, MdbRecord oldRecord, MdbRecord newRecord)
    {
        var oldId = GetIdValue(oldRecord);
        var newId = GetIdValue(newRecord);
        if (string.Equals(oldId, newId, StringComparison.Ordinal))
        {
            return;
        }

        if (oldId is not null)
        {
            RemoveOwner(_idOwners, oldId, path);
        }

        if (newId is not null)
        {
            AddOwner(_idOwners, newId, path);
        }
    }

    /// <summary>ID-based resolution (Ch.08 "Ambiguity"): a unique owner resolves; two or more owners are ambiguous; no owner is simply unresolved.</summary>
    public (string? Path, bool Ambiguous) ResolveId(string idValue)
    {
        if (_idOwners.TryGetValue(idValue, out var owners) && owners.Count > 0)
        {
            return owners.Count == 1 ? (owners[0], false) : (null, true);
        }

        return (null, false);
    }

    public IReadOnlyList<string> GetBasenameCandidates(string basename) =>
        _basenameOwners.TryGetValue(basename, out var owners) ? owners : Array.Empty<string>();

    private string? GetIdValue(MdbRecord record) =>
        record.Frontmatter.Contains(_idField) && record.Frontmatter[_idField] is string s && s.Length > 0 ? s : null;

    internal static string GetBasename(string path)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static void AddOwner(Dictionary<string, List<string>> owners, string key, string path)
    {
        if (!owners.TryGetValue(key, out var list))
        {
            list = new List<string>();
            owners[key] = list;
        }

        if (!list.Contains(path, StringComparer.Ordinal))
        {
            list.Add(path);
        }
    }

    private static void RemoveOwner(Dictionary<string, List<string>> owners, string key, string path)
    {
        if (!owners.TryGetValue(key, out var list))
        {
            return;
        }

        list.RemoveAll(p => string.Equals(p, path, StringComparison.Ordinal));
        if (list.Count == 0)
        {
            owners.Remove(key);
        }
    }
}
