using System.Collections;
using System.Collections.Specialized;
using Mdbase.Core.Cel;
using Mdbase.Core.Write;

namespace Mdbase.Core.Loading;

/// <summary>
/// Compiles a type file's `lifecycle` section (spec Ch.09) at type-load time: `on_create`/
/// `on_update` are retained (grouped by target field, in declared order, per #41 point 8);
/// `on_delete`/`on_rename` are compiled here too — for forward compatibility, so a malformed
/// declaration is still a type-load diagnostic (#41 point 11) — but discarded, since no write
/// pipeline executes them yet.
/// </summary>
internal static class LifecycleSectionLoader
{
    public static (
        IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>> OnCreate,
        IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>> OnUpdate)
        Parse(OrderedDictionary? lifecycleSection, string relativeFilePath)
    {
        var onCreate = GroupByField(ParseEvent(lifecycleSection?["on_create"], relativeFilePath, "on_create"));
        var onUpdate = GroupByField(ParseEvent(lifecycleSection?["on_update"], relativeFilePath, "on_update"));

        // Compiled-but-unexecuted (#41 point 11): validated for type-load correctness, then discarded.
        GroupByField(ParseEvent(lifecycleSection?["on_delete"], relativeFilePath, "on_delete"));
        GroupByField(ParseEvent(lifecycleSection?["on_rename"], relativeFilePath, "on_rename"));

        return (onCreate, onUpdate);
    }

    private static List<MdbLifecycleRule> ParseEvent(object? raw, string relativeFilePath, string eventName)
    {
        var groups = raw switch
        {
            null => Array.Empty<OrderedDictionary>(),
            OrderedDictionary single => new[] { single },
            object?[] list => list.Select(g => g as OrderedDictionary
                ?? throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping entry in 'lifecycle.{eventName}'.")).ToArray(),
            _ => throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has an invalid 'lifecycle.{eventName}'."),
        };

        var rules = new List<MdbLifecycleRule>();
        foreach (var group in groups)
        {
            string? guardSource = null;
            Cel.CompiledCelExpression? guard = null;
            var referencesFile = false;

            if (group.Contains("if") && group["if"] is not null)
            {
                if (group["if"] is not string ifSource)
                {
                    throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-string 'lifecycle.{eventName}.if'.");
                }

                guardSource = ifSource;
                try
                {
                    guard = CelExpressionContext.Lifecycle.Compile(ifSource);
                }
                catch (CelCompileException ex)
                {
                    throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has an invalid 'lifecycle.{eventName}.if': {ex.Message}");
                }

                referencesFile = CelAstScan.ReferencesIdentifier(guard.Ast, "file");
            }

            if (group["set"] is not OrderedDictionary setMap)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a 'lifecycle.{eventName}' entry missing a 'set' mapping.");
            }

            foreach (DictionaryEntry entry in setMap)
            {
                var field = (string)entry.Key;
                var (kind, arg) = ParseProviderDirective(entry.Value, relativeFilePath, eventName, field);
                rules.Add(new MdbLifecycleRule
                {
                    Field = field,
                    GuardSource = guardSource,
                    Guard = guard,
                    GuardReferencesFile = referencesFile,
                    ProviderKind = kind,
                    ProviderArg = arg,
                });
            }
        }

        return rules;
    }

    private static (MdbLifecycleProviderKind Kind, object? Arg) ParseProviderDirective(object? value, string relativeFilePath, string eventName, string field)
    {
        if (value is not OrderedDictionary map || map.Count != 1)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a malformed provider directive at 'lifecycle.{eventName}.set.{field}'.");
        }

        var entry = map.Cast<DictionaryEntry>().Single();
        var key = (string)entry.Key;
        return key switch
        {
            "now" => (MdbLifecycleProviderKind.Now, entry.Value),
            "today" => (MdbLifecycleProviderKind.Today, entry.Value),
            "uuid" => (MdbLifecycleProviderKind.Uuid, entry.Value),
            "ulid" => (MdbLifecycleProviderKind.Ulid, entry.Value),
            "slugify" when entry.Value is string sourceField => (MdbLifecycleProviderKind.Slugify, sourceField),
            "copy" when entry.Value is string copyField => (MdbLifecycleProviderKind.Copy, copyField),
            "literal" => (MdbLifecycleProviderKind.Literal, entry.Value),
            _ => throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has an unknown or malformed provider '{key}' at 'lifecycle.{eventName}.set.{field}'."),
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>> GroupByField(List<MdbLifecycleRule> rules)
    {
        var byField = new Dictionary<string, List<MdbLifecycleRule>>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!byField.TryGetValue(rule.Field, out var list))
            {
                list = new List<MdbLifecycleRule>();
                byField[rule.Field] = list;
            }

            list.Add(rule);
        }

        return byField.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<MdbLifecycleRule>)kv.Value);
    }
}
