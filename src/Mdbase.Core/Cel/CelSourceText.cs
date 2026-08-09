using System.Collections.Specialized;

namespace Mdbase.Core.Cel;

/// <summary>
/// Extracts stored CEL source text from its on-disk YAML wrapper (spec Ch.10 "each containing
/// object defines how source text is stored"). `match.expr` wraps its string in a single-key
/// <c>$expr</c> object (the general "object values are literal unless they have exactly one
/// <c>$expr</c> key" convention); `collection.projections`/named query projections wrap theirs
/// in an <c>expr</c> object member instead — two distinct, spec-fixed wrapper shapes.
/// </summary>
internal static class CelSourceText
{
    public static string ExtractDollarExpr(object? value, string errorContext)
    {
        if (value is not OrderedDictionary map || map.Count != 1 || map["$expr"] is not string source)
        {
            throw new ArgumentException($"{errorContext} must be an object with exactly one '$expr' string member.");
        }

        return source;
    }

    public static string ExtractExprField(object? value, string errorContext)
    {
        if (value is not OrderedDictionary map || map["expr"] is not string source)
        {
            throw new ArgumentException($"{errorContext} must be an object with a string 'expr' member.");
        }

        return source;
    }
}
