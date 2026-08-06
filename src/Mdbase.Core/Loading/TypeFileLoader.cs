using System.Collections;
using System.Collections.Specialized;
using System.Text.Json.Nodes;
using Json.Schema;
using Mdbase.Core.Json;
using Mdbase.Core.Links;
using Mdbase.Core.Matching;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Loading;

/// <summary>
/// Loads one candidate type file (spec Ch.05 "Type Evaluation Model" steps 1-5; step 6,
/// `implements` resolution against the data-contract registry, is out of scope for this spec).
/// The candidate's frontmatter is parsed exactly once by the caller and reused for both
/// `kind: mdbase.type` detection and this full compile (spec Ch.02 "Type Discovery"; #8
/// resolution point 3) — a malformed type file gets a real diagnostic here, never a silent skip.
/// </summary>
internal static class TypeFileLoader
{
    private const string TypeKind = "mdbase.type";

    /// <summary>True when this candidate's frontmatter declares it as a type file at all.</summary>
    public static bool IsTypeCandidate(OrderedDictionary frontmatter) =>
        frontmatter.Contains("kind") && frontmatter["kind"] is string kind && kind == TypeKind;

    /// <summary>
    /// Compiles a confirmed type-file candidate. Throws <see cref="TypeFileException"/> for
    /// any spec violation — the caller catches it, reports one diagnostic naming this file, and
    /// excludes the type from the registry without aborting the whole collection load.
    /// </summary>
    public static MdbType Load(OrderedDictionary frontmatter, string relativeFilePath, string collectionRoot)
    {
        if (frontmatter["name"] is not string name || name.Length == 0)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' is missing a non-empty 'name'.");
        }

        int? version = null;
        if (frontmatter.Contains("version") && frontmatter["version"] is not null)
        {
            version = frontmatter["version"] switch
            {
                long l when l > 0 => (int)l,
                _ => throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-positive-integer 'version'."),
            };
        }

        if (frontmatter["schema"] is not OrderedDictionary schemaSection)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' is missing a 'schema' section.");
        }

        var schema = CompileSchema(schemaSection, relativeFilePath, collectionRoot);

        var matchSection = frontmatter["match"] as OrderedDictionary;
        var match = matchSection is null ? CompiledMatch.None : CompiledMatch.Compile(matchSection);

        var collectionSection = frontmatter["collection"] as OrderedDictionary;
        var readDefaults = collectionSection?["read_defaults"] switch
        {
            OrderedDictionary rd => rd.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value),
            null => new Dictionary<string, object?>(),
            _ => throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping 'collection.read_defaults'."),
        };

        var linkRules = ParseLinkRules(collectionSection, relativeFilePath);

        return new MdbType
        {
            Name = name,
            FilePath = relativeFilePath,
            Version = version,
            Schema = schema,
            Match = match,
            ReadDefaults = readDefaults,
            LinkRules = linkRules,
            CollectionSection = collectionSection,
        };
    }

    private static IReadOnlyDictionary<string, LinkFieldRule> ParseLinkRules(OrderedDictionary? collectionSection, string relativeFilePath)
    {
        if (collectionSection?["links"] is not OrderedDictionary linksSection)
        {
            if (collectionSection?.Contains("links") == true && collectionSection["links"] is not null)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping 'collection.links'.");
            }

            return new Dictionary<string, LinkFieldRule>();
        }

        var rules = new Dictionary<string, LinkFieldRule>();
        foreach (DictionaryEntry entry in linksSection)
        {
            var fieldPath = (string)entry.Key;
            if (entry.Value is not OrderedDictionary ruleMap)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping 'collection.links.{fieldPath}'.");
            }

            var targetType = ruleMap.Contains("target_type") ? ruleMap["target_type"] as string : null;
            var validateExists = ruleMap.Contains("validate_exists") && ruleMap["validate_exists"] is bool b && b;

            rules[fieldPath] = new LinkFieldRule
            {
                FieldPath = fieldPath,
                TargetType = targetType,
                ValidateExists = validateExists,
            };
        }

        return rules;
    }

    private static JsonSchema CompileSchema(OrderedDictionary schemaSection, string relativeFilePath, string collectionRoot)
    {
        var dialect = schemaSection["dialect"] as string ?? "json-schema-2020-12";
        if (dialect != "json-schema-2020-12")
        {
            throw new TypeFileException(
                "unsupported_profile",
                $"Type file '{relativeFilePath}' declares unsupported schema.dialect '{dialect}'.");
        }

        var hasValue = schemaSection.Contains("value") && schemaSection["value"] is not null;
        var hasRef = schemaSection.Contains("ref") && schemaSection["ref"] is not null;
        if (hasValue == hasRef)
        {
            throw new TypeFileException(
                "type_invalid",
                $"Type file '{relativeFilePath}' must declare exactly one of schema.value or schema.ref.");
        }

        JsonNode? schemaNode;
        if (hasValue)
        {
            if (schemaSection["value"] is not OrderedDictionary valueMap)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping schema.value.");
            }

            schemaNode = JsonModel.ToJsonNode(valueMap);
        }
        else
        {
            if (schemaSection["ref"] is not string refPath)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-string schema.ref.");
            }

            schemaNode = LoadExternalSchema(refPath, relativeFilePath, collectionRoot);
        }

        RejectNonFragmentRefs(schemaNode, relativeFilePath);

        try
        {
            return JsonSchema.FromText(schemaNode!.ToJsonString());
        }
        catch (Exception ex)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a malformed JSON Schema: {ex.Message}");
        }
    }

    private static JsonNode? LoadExternalSchema(string refPath, string relativeFilePath, string collectionRoot)
    {
        var hashIndex = refPath.IndexOf('#');
        var filePart = hashIndex < 0 ? refPath : refPath[..hashIndex];
        var fragment = hashIndex < 0 ? null : refPath[(hashIndex + 1)..];

        var typeFileDir = Path.GetDirectoryName(Path.Combine(collectionRoot, relativeFilePath)) ?? collectionRoot;
        var resolved = Path.GetFullPath(Path.Combine(typeFileDir, filePart));
        var rootFull = Path.GetFullPath(collectionRoot);

        if (!resolved.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal) && resolved != rootFull)
        {
            throw new TypeFileException(
                "schema_ref_forbidden",
                $"Type file '{relativeFilePath}' has schema.ref '{refPath}' resolving outside the collection root.");
        }

        if (!File.Exists(resolved))
        {
            throw new TypeFileException(
                "schema_ref_unresolved",
                $"Type file '{relativeFilePath}' has schema.ref '{refPath}' which does not exist.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(resolved));
        }
        catch (Exception ex)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has an unparseable schema.ref file: {ex.Message}");
        }

        return string.IsNullOrEmpty(fragment) ? node : NavigateJsonPointer(node, fragment, relativeFilePath, refPath);
    }

    /// <summary>Navigates a non-root RFC 6901 JSON Pointer fragment on `schema.ref` into the parsed external document.</summary>
    private static JsonNode? NavigateJsonPointer(JsonNode? node, string fragment, string relativeFilePath, string refPath)
    {
        if (fragment[0] != '/')
        {
            throw new TypeFileException(
                "schema_ref_unresolved",
                $"Type file '{relativeFilePath}' has schema.ref '{refPath}' with a malformed JSON Pointer fragment.");
        }

        foreach (var rawToken in fragment[1..].Split('/'))
        {
            var token = rawToken.Replace("~1", "/").Replace("~0", "~");
            node = node switch
            {
                JsonObject obj when obj.ContainsKey(token) => obj[token],
                JsonArray arr when int.TryParse(token, out var index) && index >= 0 && index < arr.Count => arr[index],
                _ => throw new TypeFileException(
                    "schema_ref_unresolved",
                    $"Type file '{relativeFilePath}' has schema.ref '{refPath}' whose fragment does not resolve."),
            };
        }

        return node;
    }

    /// <summary>
    /// Nested file-to-file `$ref` (a non-fragment reference) requires the optional
    /// `external_schema_refs` feature (spec Ch.06 "References"), which this implementation
    /// does not support — reject before ever invoking a resolver.
    /// </summary>
    private static void RejectNonFragmentRefs(JsonNode? node, string relativeFilePath)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value is JsonValue v && v.TryGetValue(out string? refValue) &&
                        refValue is not null && !refValue.StartsWith('#'))
                    {
                        throw new TypeFileException(
                            "unsupported_profile",
                            $"Type file '{relativeFilePath}' uses a non-fragment $ref ('{refValue}'), which requires the external_schema_refs feature.");
                    }

                    RejectNonFragmentRefs(value, relativeFilePath);
                }

                break;

            case JsonArray arr:
                foreach (var item in arr)
                {
                    RejectNonFragmentRefs(item, relativeFilePath);
                }

                break;
        }
    }
}
