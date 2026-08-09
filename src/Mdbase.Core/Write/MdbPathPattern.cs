using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace Mdbase.Core.Write;

/// <summary>
/// A compiled `collection.path.pattern` template (spec Ch.07 "Path Policy"): literal segments
/// plus `{field}` placeholder tokens against top-level frontmatter fields, converted to strings
/// without expression evaluation.
/// </summary>
internal sealed class MdbPathPattern
{
    private abstract record Segment;

    private sealed record Literal(string Text) : Segment;

    private sealed record Placeholder(string Field) : Segment;

    private readonly IReadOnlyList<Segment> _segments;

    private MdbPathPattern(IReadOnlyList<Segment> segments, string source)
    {
        _segments = segments;
        Source = source;
    }

    /// <summary>The exact declared pattern string — the composer's path-axis equality key.</summary>
    public string Source { get; }

    /// <summary>Compiles a pattern string. Throws <see cref="FormatException"/> on unbalanced braces or an empty placeholder name.</summary>
    public static MdbPathPattern Compile(string pattern)
    {
        var segments = new List<Segment>();
        var literal = new StringBuilder();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '{')
            {
                var close = pattern.IndexOf('}', i + 1);
                if (close < 0)
                {
                    throw new FormatException($"Path pattern '{pattern}' has an unterminated '{{' placeholder.");
                }

                var field = pattern[(i + 1)..close];
                if (field.Length == 0)
                {
                    throw new FormatException($"Path pattern '{pattern}' has an empty '{{}}' placeholder.");
                }

                if (literal.Length > 0)
                {
                    segments.Add(new Literal(literal.ToString()));
                    literal.Clear();
                }

                segments.Add(new Placeholder(field));
                i = close + 1;
                continue;
            }

            if (c == '}')
            {
                throw new FormatException($"Path pattern '{pattern}' has an unmatched '}}'.");
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0)
        {
            segments.Add(new Literal(literal.ToString()));
        }

        return new MdbPathPattern(segments, pattern);
    }

    /// <summary>
    /// Generates a collection-relative path against <paramref name="draft"/>'s post-lifecycle
    /// fields. On failure returns null and sets exactly one of <paramref name="missingField"/>
    /// (a placeholder field is missing or null) or <paramref name="invalidField"/>/<paramref name="invalidValue"/>
    /// (a substituted value produces `/`, `\`, `.`, or `..`).
    /// </summary>
    public string? Generate(OrderedDictionary draft, out string? missingField, out string? invalidField, out string? invalidValue)
    {
        missingField = null;
        invalidField = null;
        invalidValue = null;
        var sb = new StringBuilder();
        foreach (var segment in _segments)
        {
            if (segment is Literal literal)
            {
                sb.Append(literal.Text);
                continue;
            }

            var placeholder = (Placeholder)segment;
            if (!draft.Contains(placeholder.Field) || draft[placeholder.Field] is null)
            {
                missingField = placeholder.Field;
                return null;
            }

            var text = ToPathComponentText(draft[placeholder.Field]);
            if (text.Length == 0 || text is "." or ".." || text.Contains('/') || text.Contains('\\'))
            {
                invalidField = placeholder.Field;
                invalidValue = text;
                return null;
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    private static string ToPathComponentText(object? value) => value switch
    {
        string s => s,
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => value?.ToString() ?? string.Empty,
    };
}
