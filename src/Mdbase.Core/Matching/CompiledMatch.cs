using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Mdbase.Core.Loading;

namespace Mdbase.Core.Matching;

/// <summary>
/// A type's compiled `match` section (spec Ch.07): `path_glob` (OR-combined), `fields_present`,
/// and `where` combine with AND. `match.expr` (CEL Match) is out of scope for this spec — its
/// presence rejects the type file with `unsupported_profile` at compile time (spec Ch.07 "A
/// type containing match.expr requires the cel_match conformance profile").
/// </summary>
internal sealed class CompiledMatch
{
    private readonly bool _hasMatchSection;
    private readonly IReadOnlyList<Regex> _pathGlobs;
    private readonly IReadOnlyList<FieldRef> _fieldsPresent;
    private readonly WherePredicate? _where;

    private CompiledMatch(
        bool hasMatchSection,
        IReadOnlyList<Regex> pathGlobs,
        IReadOnlyList<FieldRef> fieldsPresent,
        WherePredicate? where)
    {
        _hasMatchSection = hasMatchSection;
        _pathGlobs = pathGlobs;
        _fieldsPresent = fieldsPresent;
        _where = where;
    }

    public static readonly CompiledMatch None = new(false, Array.Empty<Regex>(), Array.Empty<FieldRef>(), null);

    public static CompiledMatch Compile(OrderedDictionary matchSection)
    {
        if (matchSection.Contains("expr"))
        {
            throw new TypeFileException(
                "unsupported_profile",
                "match.expr requires the cel_match conformance profile, which this implementation does not support.");
        }

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

        return new CompiledMatch(true, pathGlobs, fieldsPresent, where);
    }

    /// <summary>
    /// Evaluates this type's inferred match against a candidate record. A type with no
    /// `match` section contributes no inferred match (spec Ch.05 "Type Membership").
    /// </summary>
    public bool Matches(string relativePath, OrderedDictionary rawFrontmatter)
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

        return _where is null || _where.Evaluate(rawFrontmatter);
    }
}
