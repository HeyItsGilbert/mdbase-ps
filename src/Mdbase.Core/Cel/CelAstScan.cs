using Celly.Ast;

namespace Mdbase.Core.Cel;

/// <summary>
/// A small companion to <see cref="AstTools.ReferencedVariables"/>: finds every literal select
/// chain rooted at a known identifier — used both for the <c>present.raw</c>/<c>present.record</c>
/// presence contract (Ch.10 "Missing And Null") and for named-query-projection dependency edges
/// (<c>projection.&lt;name&gt;</c> references, Ch.11). Presence/projection access must never
/// error on an unlisted field name, so callers pre-seed exactly the field names an expression
/// can possibly select — found here statically, at compile time, using only the public
/// <see cref="AstTools.DescendantsAndSelf"/> traversal.
/// </summary>
internal static class CelAstScan
{
    /// <summary>Field names referenced via `&lt;rootIdent&gt;.&lt;midField&gt;.&lt;field&gt;` anywhere in the expression (e.g. every `present.raw.&lt;field&gt;` reference).</summary>
    public static IReadOnlySet<string> NestedSelectFields(Expr root, string rootIdent, string midField)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in AstTools.DescendantsAndSelf(root))
        {
            if (node is SelectExpr { Operand: SelectExpr { Operand: IdentExpr ident } inner } select
                && ident.Name == rootIdent && inner.Field == midField)
            {
                result.Add(select.Field);
            }
        }

        return result;
    }

    /// <summary>Field names directly selected off `&lt;rootIdent&gt;.&lt;field&gt;` (e.g. every `projection.&lt;name&gt;` reference) — used to derive named-query-projection dependency edges.</summary>
    public static IReadOnlySet<string> SelectFields(Expr root, string rootIdent)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in AstTools.DescendantsAndSelf(root))
        {
            if (node is SelectExpr { Operand: IdentExpr ident } select && ident.Name == rootIdent)
            {
                result.Add(select.Field);
            }
        }

        return result;
    }
}
