using System.Collections;
using System.Collections.Specialized;
using Celly.Values;

namespace Mdbase.Core.Cel;

/// <summary>
/// Converts between mdbase's JSON data model (<see cref="Mdbase.Core.Json.JsonModel"/>'s
/// null/bool/long/double/string/<see cref="OrderedDictionary"/>/<c>object?[]</c> shape) and
/// Celly's <see cref="CelValue"/> tree — the one conversion boundary every CEL binding in this
/// module (match, projections, query) shares.
/// </summary>
internal static class CelValueConversion
{
    public static CelValue ToCelValue(object? value) => value switch
    {
        null => NullValue.Instance,
        CelValue already => already,
        bool b => BoolValue.Of(b),
        long l => IntValue.Of(l),
        int i => IntValue.Of(i),
        double d => DoubleValue.Of(d),
        string s => StringValue.Of(s),
        OrderedDictionary map => MapValue.Build(map.Cast<DictionaryEntry>()
            .Select(entry => new KeyValuePair<CelValue, CelValue>(StringValue.Of((string)entry.Key), ToCelValue(entry.Value)))),
        IEnumerable<object?> arr => ListValue.Of(arr.Select(ToCelValue).ToArray()),
        _ => throw new NotSupportedException($"Value of type {value.GetType()} is not part of the mdbase CEL data model."),
    };

    /// <summary>Converts an evaluated <see cref="CelValue"/> back into the mdbase JSON data model, for a query result/select/summary output.</summary>
    public static object? ToMdbValue(CelValue value) => value switch
    {
        NullValue => null,
        BoolValue b => b.Value,
        IntValue i => i.Value,
        UintValue u => checked((long)u.Value),
        DoubleValue d => d.Value,
        StringValue s => s.Value,
        BytesValue by => by.ToByteArray(),
        TimestampValue ts => DateTimeOffset.FromUnixTimeSeconds(ts.Data.Seconds).AddTicks(ts.Data.Nanos / 100).ToString("O"),
        DurationValue du => FormatDuration(du),
        MapValue map => ToOrderedDictionary(map),
        ListValue list => list.Elements.Select(ToMdbValue).ToArray(),
        _ => value.ToNative(),
    };

    private static OrderedDictionary ToOrderedDictionary(MapValue map)
    {
        var result = new OrderedDictionary();
        foreach (var key in map.Keys)
        {
            if (map.TryGet(key, out var value))
            {
                result[(string)ToMdbValue(key)!] = ToMdbValue(value);
            }
        }

        return result;
    }

    private static string FormatDuration(DurationValue duration)
    {
        var totalSeconds = duration.Data.Seconds + duration.Data.Nanos / 1_000_000_000.0;
        return TimeSpan.FromSeconds(totalSeconds).ToString();
    }
}
