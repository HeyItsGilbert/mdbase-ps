using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Mdbase.Core.Json;

namespace Mdbase.Core.Matching;

/// <summary>
/// A compiled `match.where` structured predicate (spec Ch.07 "Structured Predicates"): a
/// mapping of field references combined with AND, each resolving to either a direct-value
/// deep-equality check or an AND-combined map of operators.
/// </summary>
internal sealed class WherePredicate
{
    private static readonly IReadOnlyCollection<string> OperatorNames = new HashSet<string>
    {
        "eq", "neq", "gt", "gte", "lt", "lte", "contains", "containsAll", "containsAny",
        "startsWith", "endsWith", "matches", "exists",
    };

    private readonly IReadOnlyList<(FieldRef Field, IReadOnlyList<(string Op, object? Operand)> Operators)> _clauses;

    private WherePredicate(IReadOnlyList<(FieldRef, IReadOnlyList<(string, object?)>)> clauses)
    {
        _clauses = clauses;
    }

    public static WherePredicate Compile(OrderedDictionary where)
    {
        var clauses = new List<(FieldRef, IReadOnlyList<(string, object?)>)>();
        foreach (DictionaryEntry entry in where)
        {
            var field = FieldRef.Parse((string)entry.Key);
            var operators = entry.Value is OrderedDictionary map && IsOperatorMap(map)
                ? map.Cast<DictionaryEntry>().Select(e => ((string)e.Key, e.Value)).ToList()
                : new List<(string, object?)> { ("eq", entry.Value) };

            foreach (var (op, _) in operators)
            {
                if (!OperatorNames.Contains(op))
                {
                    throw new ArgumentException($"Unknown match.where operator '{op}'.");
                }
            }

            // Fail fast on an invalid regex, matching the eager-compile pattern used elsewhere in #7/#8.
            foreach (var (op, operand) in operators.Where(o => o.Item1 == "matches"))
            {
                _ = ToRegex(operand);
            }

            clauses.Add((field, operators));
        }

        return new WherePredicate(clauses);
    }

    private static bool IsOperatorMap(OrderedDictionary map) =>
        map.Count > 0 && map.Cast<DictionaryEntry>().All(e => OperatorNames.Contains((string)e.Key));

    public bool Evaluate(OrderedDictionary frontmatter)
    {
        foreach (var (field, operators) in _clauses)
        {
            var (exists, value) = field.Resolve(frontmatter);
            foreach (var (op, operand) in operators)
            {
                if (op == "exists")
                {
                    if (exists != (bool)(operand ?? throw new ArgumentException("'exists' requires a boolean operand.")))
                    {
                        return false;
                    }

                    continue;
                }

                if (!exists || value is null)
                {
                    return false;
                }

                if (!EvaluateOperator(op, value, operand))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool EvaluateOperator(string op, object value, object? operand) => op switch
    {
        "eq" => JsonModel.DeepEquals(value, operand),
        "neq" => !JsonModel.DeepEquals(value, operand),
        "gt" => Compare(value, operand) is { } c && c > 0,
        "gte" => Compare(value, operand) is { } c && c >= 0,
        "lt" => Compare(value, operand) is { } c && c < 0,
        "lte" => Compare(value, operand) is { } c && c <= 0,
        "contains" => Contains(value, operand),
        "containsAll" => value is object?[] arr && operand is object?[] needles &&
                          needles.All(n => arr.Any(v => JsonModel.DeepEquals(v, n))),
        "containsAny" => value is object?[] arr2 && operand is object?[] needles2 &&
                          needles2.Any(n => arr2.Any(v => JsonModel.DeepEquals(v, n))),
        "startsWith" => value is string s1 && operand is string p1 && s1.StartsWith(p1, StringComparison.Ordinal),
        "endsWith" => value is string s2 && operand is string p2 && s2.EndsWith(p2, StringComparison.Ordinal),
        "matches" => value is string s3 && ToRegex(operand).IsMatch(s3),
        _ => false,
    };

    private static bool Contains(object value, object? operand) => value switch
    {
        string s when operand is string needle => s.Contains(needle, StringComparison.Ordinal),
        object?[] arr => arr.Any(v => JsonModel.DeepEquals(v, operand)),
        _ => false,
    };

    private static int? Compare(object value, object? operand)
    {
        if (value is (long or double) && operand is (long or double))
        {
            return Convert.ToDouble(value).CompareTo(Convert.ToDouble(operand));
        }

        if (value is string sv && operand is string so)
        {
            return string.CompareOrdinal(sv, so);
        }

        return null;
    }

    private static Regex ToRegex(object? operand) => operand is string pattern
        ? new Regex(pattern, RegexOptions.Compiled)
        : throw new ArgumentException("'matches' requires a string pattern operand.");
}
