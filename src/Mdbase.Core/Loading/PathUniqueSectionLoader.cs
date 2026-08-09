using System.Collections.Specialized;
using Mdbase.Core.Matching;
using Mdbase.Core.Write;

namespace Mdbase.Core.Loading;

/// <summary>
/// Compiles a type file's `collection.path` (spec Ch.07 "Path Policy") and `collection.unique`
/// (spec Ch.07 "Cross-File Uniqueness") sections at type-load time — the write half of
/// Collection Semantics #37 explicitly deferred.
/// </summary>
internal static class PathUniqueSectionLoader
{
    public static MdbPathPattern? ParsePathPattern(OrderedDictionary? collectionSection, string relativeFilePath)
    {
        if (collectionSection?["path"] is not OrderedDictionary pathSection)
        {
            if (collectionSection?.Contains("path") == true && collectionSection["path"] is not null)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-mapping 'collection.path'.");
            }

            return null;
        }

        // A `collection.path` mapping without a `pattern` key is a non-portable, vendor/runtime-
        // specific shape (e.g. a migrated tool's own `folder`/`template`/`generated_by` keys) —
        // not a violation of the portable Ch.07 grammar. Only a present-but-non-string `pattern`
        // is malformed.
        if (!pathSection.Contains("pattern"))
        {
            return null;
        }

        if (pathSection["pattern"] is not string pattern || pattern.Length == 0)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-string 'collection.path.pattern'.");
        }

        try
        {
            return MdbPathPattern.Compile(pattern);
        }
        catch (FormatException ex)
        {
            throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has an invalid 'collection.path.pattern': {ex.Message}");
        }
    }

    public static IReadOnlyList<MdbUniqueRule> ParseUniqueRules(OrderedDictionary? collectionSection, string relativeFilePath)
    {
        if (collectionSection?["unique"] is not object?[] list)
        {
            if (collectionSection?.Contains("unique") == true && collectionSection["unique"] is not null)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a non-list 'collection.unique'.");
            }

            return Array.Empty<MdbUniqueRule>();
        }

        var rules = new List<MdbUniqueRule>();
        foreach (var item in list)
        {
            if (item is not OrderedDictionary rule || rule["field"] is not string field || field.Length == 0)
            {
                throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a 'collection.unique' entry missing a string 'field'.");
            }

            var scopeText = rule["scope"] as string ?? "collection";
            var scope = scopeText switch
            {
                "collection" => MdbUniqueScope.Collection,
                "type" => MdbUniqueScope.Type,
                "path_glob" => MdbUniqueScope.PathGlob,
                _ => throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a 'collection.unique' entry with unknown scope '{scopeText}'."),
            };

            string? pathGlob = null;
            System.Text.RegularExpressions.Regex? compiledGlob = null;
            if (scope == MdbUniqueScope.PathGlob)
            {
                if (rule["path_glob"] is not string glob || glob.Length == 0)
                {
                    throw new TypeFileException("type_invalid", $"Type file '{relativeFilePath}' has a 'path_glob'-scoped 'collection.unique' entry missing 'path_glob'.");
                }

                pathGlob = glob;
                compiledGlob = GlobPattern.Compile(glob);
            }

            rules.Add(new MdbUniqueRule { Field = field, Scope = scope, PathGlob = pathGlob, CompiledPathGlob = compiledGlob });
        }

        return rules;
    }
}
