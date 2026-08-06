using System.Collections.Specialized;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Configuration;

/// <summary>The resolved `mdbase.yaml` settings for a collection (spec Ch.04).</summary>
public sealed record MdbCollectionConfig
{
    public required string SpecVersion { get; init; }

    public string TypesFolder { get; init; } = "_types";

    public string ContractsFolder { get; init; } = "_contracts";

    public IReadOnlyList<string> RecordExtensions { get; init; } = new[] { "md" };

    public string Validation { get; init; } = "error";

    public IReadOnlyList<string> ExplicitTypeKeys { get; init; } = new[] { "type", "types" };

    public string IdField { get; init; } = "id";

    public bool IncludeSubfolders { get; init; } = true;

    public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Parses `mdbase.yaml` text into a config. Only the v0.3 core settings needed by the
    /// read path (spec Ch.04) are decomposed; unknown keys are ignored here (a Core Read
    /// implementation only warns on them, it does not need to round-trip them for this slice).
    /// </summary>
    public static MdbCollectionConfig Parse(string yamlText)
    {
        var root = FrontmatterParser.ParseYamlMapping(yamlText);

        if (!root.Contains("spec_version") || root["spec_version"] is not string specVersion || specVersion.Length == 0)
        {
            throw new FrontmatterParseException("mdbase.yaml MUST declare a string 'spec_version'.");
        }

        if (!specVersion.StartsWith("0.3", StringComparison.Ordinal))
        {
            throw new FrontmatterParseException(
                $"Unsupported spec_version '{specVersion}'; this implementation supports v0.3 collections only.");
        }

        var settings = root["settings"] as OrderedDictionary;

        return new MdbCollectionConfig
        {
            SpecVersion = specVersion,
            TypesFolder = NormalizeFolder(GetString(settings, "types_folder") ?? "_types"),
            ContractsFolder = NormalizeFolder(GetString(settings, "contracts_folder") ?? "_contracts"),
            RecordExtensions = GetStringList(settings, "record_extensions") ?? new[] { "md" },
            Validation = GetString(settings, "validation") ?? "error",
            ExplicitTypeKeys = GetStringList(settings, "explicit_type_keys") ?? new[] { "type", "types" },
            IdField = GetString(settings, "id_field") ?? "id",
            IncludeSubfolders = GetBool(settings, "include_subfolders") ?? true,
            Exclude = GetStringList(settings, "exclude") ?? Array.Empty<string>(),
        };
    }

    private static string NormalizeFolder(string folder) => folder.Trim('/');

    private static string? GetString(OrderedDictionary? map, string key) =>
        map is not null && map.Contains(key) && map[key] is string s ? s : null;

    private static bool? GetBool(OrderedDictionary? map, string key) =>
        map is not null && map.Contains(key) && map[key] is bool b ? b : null;

    private static IReadOnlyList<string>? GetStringList(OrderedDictionary? map, string key)
    {
        if (map is null || !map.Contains(key) || map[key] is not object?[] items)
        {
            return null;
        }

        return items.Select(i => i as string ?? string.Empty).ToArray();
    }
}
