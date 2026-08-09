using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Mdbase.Core.Cel;
using Mdbase.Core.Loading;

namespace Mdbase.Core.Matching;

/// <summary>
/// A type's compiled `match` section (spec Ch.07): `path_glob` (OR-combined), `fields_present`,
/// `where`, and `expr` (CEL Match, Ch.07 "CEL Matching") all combine with AND.
/// </summary>
internal sealed class CompiledMatch
{
    private readonly bool _hasMatchSection;
    private readonly IReadOnlyList<Regex> _pathGlobs;
    private readonly IReadOnlyList<FieldRef> _fieldsPresent;
    private readonly WherePredicate? _where;
    private readonly CompiledCelExpression? _expr;
    private readonly string _exprSource = string.Empty;

    private CompiledMatch(
        bool hasMatchSection,
        IReadOnlyList<Regex> pathGlobs,
        IReadOnlyList<FieldRef> fieldsPresent,
        WherePredicate? where,
        CompiledCelExpression? expr,
        string exprSource)
    {
        _hasMatchSection = hasMatchSection;
        _pathGlobs = pathGlobs;
        _fieldsPresent = fieldsPresent;
        _where = where;
        _expr = expr;
        _exprSource = exprSource;
    }

    public static readonly CompiledMatch None = new(false, Array.Empty<Regex>(), Array.Empty<FieldRef>(), null, null, string.Empty);

    public static CompiledMatch Compile(OrderedDictionary matchSection)
    {
        var pathGlobs = matchSection["path_glob"] switch
        {
            null => Array.Empty<Regex>(),
            string single => new[] { GlobPattern.Compile(single) },
            object?[] many => many.Select(p => GlobPattern.Compile((string)p!)).ToArray(),
            _ => throw new TypeFileException("type_invalid", "match.path_glob must be a string or list of strings."),
        };

        var fieldsPresent = matchSection["fields_present"] switch
        {
            null => Array.Empty<FieldRef>(),
            object?[] refs => refs.Select(r => FieldRef.Parse((string)r!)).ToArray(),
            _ => throw new TypeFileException("type_invalid", "match.fields_present must be a list of field references."),
        };

        var where = matchSection["where"] switch
        {
            null => null,
            OrderedDictionary map => WherePredicate.Compile(map),
            _ => throw new TypeFileException("type_invalid", "match.where must be a mapping."),
        };

        CompiledCelExpression? expr = null;
        var exprSource = string.Empty;
        if (matchSection["expr"] is not null)
        {
            try
            {
                exprSource = Cel.CelSourceText.ExtractDollarExpr(matchSection["expr"], "match.expr");
            }
            catch (ArgumentException ex)
            {
                throw new TypeFileException("type_invalid", ex.Message);
            }

            try
            {
                expr = CelExpressionContext.Match.Compile(exprSource);
            }
            catch (CelCompileException ex)
            {
                throw new TypeFileException("type_invalid", $"match.expr is invalid: {ex.Message}");
            }
        }

        return new CompiledMatch(true, pathGlobs, fieldsPresent, where, expr, exprSource);
    }

    /// <summary>
    /// Evaluates this type's inferred match against a candidate record. A type with no
    /// `match` section contributes no inferred match (spec Ch.05 "Type Membership"). A
    /// `match.expr` evaluation error yields non-match for this candidate/type and appends a
    /// `match_expr_error` diagnostic — it never throws and never affects any other candidate
    /// or type (Ch.07 "CEL Matching").
    /// </summary>
    public bool Matches(
        string relativePath,
        OrderedDictionary rawFrontmatter,
        Cel.MdbFileCel file,
        string typeName,
        List<MdbDiagnostic> diagnostics)
    {
        if (!_hasMatchSection)
        {
            return false;
        }

        if (_pathGlobs.Count > 0 && !_pathGlobs.Any(g => g.IsMatch(relativePath)))
        {
            return false;
        }

        if (_fieldsPresent.Count > 0 && !_fieldsPresent.All(f => f.IsPresent(rawFrontmatter)))
        {
            return false;
        }

        if (_where is not null && !_where.Evaluate(rawFrontmatter))
        {
            return false;
        }

        if (_expr is null)
        {
            return true;
        }

        try
        {
            var activation = MdbActivation.ForMatch(rawFrontmatter, file, _expr.PresentFields, _expr.FreeIdentifiers, _expr.ReferencedTopLevelFields);
            var result = _expr.Program.Eval(activation);
            if (result.IsError)
            {
                diagnostics.Add(new MdbDiagnostic
                {
                    Severity = MdbSeverity.Warning,
                    Code = "match_expr_error",
                    Message = $"match.expr '{_exprSource}' failed to evaluate for '{relativePath}': {result}",
                    Path = relativePath,
                    Type = typeName,
                });
                return false;
            }

            return result is Celly.Values.BoolValue { Value: true };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            diagnostics.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Warning,
                Code = "match_expr_error",
                Message = $"match.expr '{_exprSource}' failed to evaluate for '{relativePath}': {ex.Message}",
                Path = relativePath,
                Type = typeName,
            });
            return false;
        }
    }
}
