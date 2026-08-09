using Mdbase.Core.Cel;
using Mdbase.Core.Matching;

namespace Mdbase.Core.Query;

/// <summary>
/// Resolves one `order_by`/`group_by`/`summaries` field reference (Ch.11) against a candidate:
/// `file.*` well-known accessors, then a named `select` output, then a named query projection,
/// then an ordinary effective-frontmatter field reference — in that order.
/// </summary>
internal static class QueryFieldResolver
{
    public static object? Resolve(
        string field,
        MdbRecord record,
        string collectionRoot,
        IReadOnlyDictionary<string, object?> selectValues,
        IReadOnlyDictionary<string, object?> projectionValues)
    {
        if (field.StartsWith("file.", StringComparison.Ordinal))
        {
            return ResolveFile(field[5..], record, collectionRoot);
        }

        if (selectValues.TryGetValue(field, out var selectValue))
        {
            return selectValue;
        }

        if (projectionValues.TryGetValue(field, out var projectionValue))
        {
            return projectionValue;
        }

        var (exists, value) = FieldRef.Parse(field).Resolve(record.EffectiveFrontmatter);
        return exists ? value : null;
    }

    private static object? ResolveFile(string accessor, MdbRecord record, string collectionRoot)
    {
        switch (accessor)
        {
            case "path": return record.FileInfo.Path;
            case "name": return record.FileInfo.Name;
            case "folder": return record.FileInfo.Directory;
        }

        var file = MdbFileCel.Build(collectionRoot, record.FileInfo.Path, record.Body);
        return accessor switch
        {
            "basename" => file.Basename,
            "ext" => file.Ext,
            "size" => file.Size,
            "mtime" => file.Mtime.ToString("O"),
            "ctime" => file.Ctime.ToString("O"),
            "body" => file.Body,
            _ => null,
        };
    }
}
