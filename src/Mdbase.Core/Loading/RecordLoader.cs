using System.Collections;
using System.Collections.Specialized;
using System.Text.Json;
using Json.Schema;
using Mdbase.Core.Compose;
using Mdbase.Core.Json;
using Mdbase.Core.Links;

namespace Mdbase.Core.Loading;

/// <summary>
/// Builds one <see cref="MdbRecord"/> snapshot for a candidate record (spec Ch.05 "Type
/// Evaluation Model" record-evaluation phase, steps 3-5): independent per-matched-type schema
/// validation, then eager <c>EffectiveFrontmatter</c>/<c>Present</c>/<c>CompositionDiagnostics</c>
/// computation (#7/#10/#34).
/// </summary>
internal static class RecordLoader
{
    private static readonly IEqualityComparer<object?> DeepEqualityComparer = new DeepEqualsComparer();

    public static MdbRecord Load(
        string relativePath,
        OrderedDictionary rawFrontmatter,
        string body,
        string revision,
        IReadOnlyList<MdbType> matchedTypes,
        IReadOnlyList<MdbDiagnostic>? extraDiagnostics = null)
    {
        var (isValid, validationDiagnostics) = ValidateSchemas(rawFrontmatter, relativePath, matchedTypes);

        if (extraDiagnostics is { Count: > 0 })
        {
            isValid = false;
            validationDiagnostics = validationDiagnostics.Concat(extraDiagnostics).ToList();
        }

        var missingKeys = matchedTypes
            .SelectMany(t => t.ReadDefaults.Keys)
            .Where(key => !rawFrontmatter.Contains(key))
            .ToHashSet(StringComparer.Ordinal);

        var (coalesced, compositionDiagnostics) = TypeConflictComposer.Compose(
            matchedTypes,
            type => (IReadOnlyDictionary<string, object?>)type.ReadDefaults
                .Where(kv => missingKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            DeepEqualityComparer,
            relativePath);

        // Composed again (discarding the diagnostics side) by LinkIndexer in phase 3, once the
        // full record inventory is available for resolution — MdbRecord has no field to cache
        // the coalesced rules across phases, and re-composing per record load/refresh is cheap.
        var (_, linkRuleConflicts) = TypeConflictComposer.Compose(
            matchedTypes,
            type => type.LinkRules,
            EqualityComparer<LinkFieldRule>.Default,
            relativePath);
        if (linkRuleConflicts.Count > 0)
        {
            compositionDiagnostics = compositionDiagnostics.Concat(linkRuleConflicts).ToList();
        }

        var effective = Clone(rawFrontmatter);
        foreach (var (key, value) in coalesced)
        {
            effective[key] = value;
        }

        var present = BuildPresent(rawFrontmatter, effective, matchedTypes);

        return new MdbRecord
        {
            FileInfo = new MdbFileInfo { Path = relativePath },
            Frontmatter = rawFrontmatter,
            EffectiveFrontmatter = effective,
            Present = present,
            Body = body,
            Revision = revision,
            MatchedTypes = matchedTypes,
            IsValid = isValid,
            ValidationDiagnostics = validationDiagnostics,
            CompositionDiagnostics = compositionDiagnostics,
            // Filled in during MdbCollection's phase 3 (#9); a phase-2-only snapshot never
            // escapes MdbCollection.LoadSingleRecord before phase 3 replaces it.
            Links = Array.Empty<MdbLink>(),
            Embeds = Array.Empty<MdbLink>(),
            Tags = Array.Empty<string>(),
            LinkDiagnostics = Array.Empty<MdbDiagnostic>(),
        };
    }

    private static (bool IsValid, IReadOnlyList<MdbDiagnostic> Diagnostics) ValidateSchemas(
        OrderedDictionary rawFrontmatter, string relativePath, IReadOnlyList<MdbType> matchedTypes)
    {
        var element = JsonModel.ToJsonNode(rawFrontmatter)!.Deserialize<JsonElement>();
        var diagnostics = new List<MdbDiagnostic>();
        var isValid = true;

        foreach (var type in matchedTypes)
        {
            var results = type.Schema.Evaluate(
                element,
                new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
            if (results.IsValid)
            {
                continue;
            }

            isValid = false;
            CollectSchemaDiagnostics(results, relativePath, type.Name, diagnostics);
        }

        return (isValid, diagnostics);
    }

    private static readonly System.Text.RegularExpressions.Regex QuotedNamePattern =
        new("\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void CollectSchemaDiagnostics(
        EvaluationResults results, string relativePath, string typeName, List<MdbDiagnostic> diagnostics)
    {
        if (results.Errors is { Count: > 0 })
        {
            var field = results.InstanceLocation.ToString().TrimStart('/');
            foreach (var (keyword, message) in results.Errors)
            {
                // `required`/`additionalProperties` name every offending property in one
                // message at the parent object's instance location; split them into one
                // diagnostic per property so `field` names the exact property, per Ch.16's
                // canonical diagnostic shape.
                if (keyword is "required" or "additionalProperties")
                {
                    foreach (System.Text.RegularExpressions.Match m in QuotedNamePattern.Matches(message))
                    {
                        diagnostics.Add(BuildDiagnostic(
                            keyword, message, relativePath, typeName, results.SchemaLocation.ToString(),
                            field.Length == 0 ? m.Groups[1].Value : $"{field}/{m.Groups[1].Value}"));
                    }

                    continue;
                }

                diagnostics.Add(BuildDiagnostic(
                    keyword, message, relativePath, typeName, results.SchemaLocation.ToString(),
                    field.Length == 0 ? null : field));
            }
        }

        if (results.Details is null)
        {
            return;
        }

        foreach (var detail in results.Details)
        {
            CollectSchemaDiagnostics(detail, relativePath, typeName, diagnostics);
        }
    }

    private static MdbDiagnostic BuildDiagnostic(
        string keyword, string message, string relativePath, string typeName, string schemaLocation, string? field) => new()
    {
        Severity = MdbSeverity.Error,
        // Ch.06 "Format" names `format_invalid` as its own diagnostic code, not the
        // generic `schema_<keyword>` mapping every other required keyword uses.
        Code = keyword switch
        {
            "" => "schema_invalid",
            "format" => "format_invalid",
            _ => $"schema_{ToSnakeCase(keyword)}",
        },
        Message = message,
        Path = relativePath,
        Field = field,
        Type = typeName,
        SchemaLocation = schemaLocation,
    };


    private static string ToSnakeCase(string camelCase)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in camelCase)
        {
            if (char.IsUpper(c))
            {
                if (sb.Length > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static MdbPresent BuildPresent(OrderedDictionary raw, OrderedDictionary effective, IReadOnlyList<MdbType> matchedTypes)
    {
        var keys = new List<string>();
        foreach (DictionaryEntry entry in raw)
        {
            keys.Add((string)entry.Key);
        }

        foreach (var key in matchedTypes.SelectMany(t => t.ReadDefaults.Keys))
        {
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        var fields = new Dictionary<string, MdbPresentState>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            fields[key] = raw.Contains(key)
                ? raw[key] is null ? MdbPresentState.Null : MdbPresentState.Raw
                : effective.Contains(key) ? MdbPresentState.Effective : MdbPresentState.Missing;
        }

        return new MdbPresent(fields);
    }

    private static OrderedDictionary Clone(OrderedDictionary source)
    {
        var clone = new OrderedDictionary();
        foreach (DictionaryEntry entry in source)
        {
            clone[entry.Key] = entry.Value;
        }

        return clone;
    }

    private sealed class DeepEqualsComparer : IEqualityComparer<object?>
    {
        bool IEqualityComparer<object?>.Equals(object? x, object? y) => JsonModel.DeepEquals(x, y);

        int IEqualityComparer<object?>.GetHashCode(object? obj) => 0;
    }
}
