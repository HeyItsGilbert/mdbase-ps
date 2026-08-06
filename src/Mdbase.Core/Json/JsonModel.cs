using System.Collections;
using System.Collections.Specialized;
using System.Text.Json.Nodes;

namespace Mdbase.Core.Json;

/// <summary>
/// Helpers for mdbase's in-memory JSON data-model shape (spec Ch.06 "Data Model"):
/// mapping -&gt; <see cref="OrderedDictionary"/> (string keys), sequence -&gt; <c>object?[]</c>,
/// scalar -&gt; <see cref="string"/>/<see cref="long"/>/<see cref="double"/>/<see cref="bool"/>/<c>null</c>.
/// No bespoke wrapper type carries these values (decided in #7/#27).
/// </summary>
public static class JsonModel
{
    /// <summary>
    /// Deep JSON-value equality over the JSON data model, used by <c>match.where</c>'s
    /// <c>eq</c>/<c>neq</c> operators and the read-defaults type-conflict composer (#34).
    /// </summary>
    public static bool DeepEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is OrderedDictionary leftMap && right is OrderedDictionary rightMap)
        {
            if (leftMap.Count != rightMap.Count)
            {
                return false;
            }

            foreach (DictionaryEntry entry in leftMap)
            {
                var key = (string)entry.Key;
                if (!rightMap.Contains(key) || !DeepEquals(entry.Value, rightMap[key]))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is object?[] leftArr && right is object?[] rightArr)
        {
            if (leftArr.Length != rightArr.Length)
            {
                return false;
            }

            for (var i = 0; i < leftArr.Length; i++)
            {
                if (!DeepEquals(leftArr[i], rightArr[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            return Convert.ToDouble(left) == Convert.ToDouble(right);
        }

        return left.Equals(right);
    }

    private static bool IsNumber(object value) => value is long or double;

    /// <summary>Converts an mdbase JSON-model value into a <see cref="JsonNode"/> for JsonSchema.Net.</summary>
    public static JsonNode? ToJsonNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case bool b:
                return JsonValue.Create(b);
            case long l:
                return JsonValue.Create(l);
            case double d:
                return JsonValue.Create(d);
            case string s:
                return JsonValue.Create(s);
            case OrderedDictionary map:
            {
                var obj = new JsonObject();
                foreach (DictionaryEntry entry in map)
                {
                    obj[(string)entry.Key] = ToJsonNode(entry.Value);
                }

                return obj;
            }
            case object?[] arr:
            {
                var jsonArray = new JsonArray();
                foreach (var item in arr)
                {
                    jsonArray.Add(ToJsonNode(item));
                }

                return jsonArray;
            }
            default:
                throw new NotSupportedException($"Value of type {value.GetType()} is not part of the mdbase JSON data model.");
        }
    }
}
