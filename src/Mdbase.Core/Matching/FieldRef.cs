using System.Collections.Specialized;

namespace Mdbase.Core.Matching;

/// <summary>
/// A collection-semantics field reference (spec Ch.07 "Field References"): either the mdbase
/// dot-path form (`title`, `metadata.owner`) or a non-root RFC 6901 JSON Pointer
/// (`/metadata/owner`). The `[]` array-item selector and per-item link application are a
/// Links-chapter concern (Ch.08) and are not resolved by this single-value reference — no
/// match/read-defaults example in scope for this spec uses it.
/// </summary>
internal sealed class FieldRef
{
    private readonly IReadOnlyList<string> _segments;

    private FieldRef(IReadOnlyList<string> segments)
    {
        _segments = segments;
    }

    public static FieldRef Parse(string reference)
    {
        if (reference.Length == 0)
        {
            throw new ArgumentException("Field reference MUST NOT be empty.", nameof(reference));
        }

        if (reference[0] == '/')
        {
            var tokens = reference[1..]
                .Split('/')
                .Select(t => t.Replace("~1", "/").Replace("~0", "~"))
                .ToArray();
            return new FieldRef(tokens);
        }

        return new FieldRef(reference.Split('.'));
    }

    /// <summary>
    /// Resolves this reference against raw persisted frontmatter. <c>Exists</c> is raw key
    /// presence (true even for an explicit null); <c>Value</c> is the resolved value.
    /// </summary>
    public (bool Exists, object? Value) Resolve(OrderedDictionary frontmatter)
    {
        object? current = frontmatter;
        foreach (var segment in _segments)
        {
            switch (current)
            {
                case OrderedDictionary map:
                    if (!map.Contains(segment))
                    {
                        return (false, null);
                    }

                    current = map[segment];
                    break;

                case object?[] array:
                    if (!int.TryParse(segment, out var index) || index < 0 || index >= array.Length)
                    {
                        return (false, null);
                    }

                    current = array[index];
                    break;

                default:
                    return (false, null);
            }
        }

        return (true, current);
    }

    /// <summary>Presence per spec Ch.07: a raw, non-null value. Missing or explicit null are not present.</summary>
    public bool IsPresent(OrderedDictionary frontmatter)
    {
        var (exists, value) = Resolve(frontmatter);
        return exists && value is not null;
    }
}
