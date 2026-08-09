namespace Mdbase.Core.Query;

/// <summary>
/// The nine portable built-in summary identifiers (Ch.11 "Grouping And Summaries"): native
/// delegates over an ordered per-group value column, each returning either a result value or a
/// `summary_incompatible_value` diagnostic reason when the column can't support the requested
/// aggregation.
/// </summary>
internal static class QuerySummaryFunctions
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        "count", "sum", "average", "minimum", "maximum", "earliest", "latest", "empty", "filled",
    };

    public static (object? Result, string? Error) Evaluate(string function, IReadOnlyList<object?> values) => function switch
    {
        "count" => (values.Count, null),
        "empty" => (values.Count(IsEmpty), null),
        "filled" => (values.Count(v => !IsEmpty(v)), null),
        "sum" => Sum(values),
        "average" => Average(values),
        "minimum" or "earliest" => MinMax(values, wantMax: false),
        "maximum" or "latest" => MinMax(values, wantMax: true),
        _ => (null, $"unknown built-in summary function '{function}'"),
    };

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => s.Length == 0,
        object?[] arr => arr.Length == 0,
        _ => false,
    };

    private static bool TryNumeric(object? value, out double number)
    {
        switch (value)
        {
            case long l: number = l; return true;
            case double d: number = d; return true;
            case int i: number = i; return true;
            default: number = 0; return false;
        }
    }

    private static (object? Result, string? Error) Sum(IReadOnlyList<object?> values)
    {
        double total = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            if (!TryNumeric(value, out var number))
            {
                return (null, $"'sum' requires numeric values, found {value.GetType().Name}.");
            }

            total += number;
        }

        return (total, null);
    }

    private static (object? Result, string? Error) Average(IReadOnlyList<object?> values)
    {
        double total = 0;
        var count = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            if (!TryNumeric(value, out var number))
            {
                return (null, $"'average' requires numeric values, found {value.GetType().Name}.");
            }

            total += number;
            count++;
        }

        return count == 0 ? (null, null) : (total / count, null);
    }

    private static (object? Result, string? Error) MinMax(IReadOnlyList<object?> values, bool wantMax)
    {
        object? best = null;
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            if (best is null)
            {
                best = value;
                continue;
            }

            int comparison;
            try
            {
                comparison = Compare(best, value);
            }
            catch (ArgumentException ex)
            {
                return (null, ex.Message);
            }

            if (wantMax ? comparison < 0 : comparison > 0)
            {
                best = value;
            }
        }

        return (best, null);
    }

    private static int Compare(object left, object right)
    {
        if (TryNumeric(left, out var leftNumber) && TryNumeric(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (left is string leftString && right is string rightString)
        {
            return string.CompareOrdinal(leftString, rightString);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool.CompareTo(rightBool);
        }

        throw new ArgumentException($"'minimum'/'maximum'/'earliest'/'latest' cannot compare {left.GetType().Name} with {right.GetType().Name}.");
    }
}
