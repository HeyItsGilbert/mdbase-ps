using System.Collections.Specialized;
using YamlDotNet.RepresentationModel;

namespace Mdbase.Core.Yaml;

/// <summary>The split of a Markdown record document into frontmatter and body (spec Ch.03).</summary>
internal sealed record ParsedDocument(OrderedDictionary Frontmatter, string Body);

/// <summary>
/// Splits Markdown-record source text into raw persisted frontmatter and body (spec Ch.03
/// "Markdown Record Structure"), then converts the YAML frontmatter block into the mdbase
/// JSON data model via <see cref="YamlJsonConverter"/>.
/// </summary>
internal static class FrontmatterParser
{
    public static ParsedDocument Parse(string source)
    {
        source = StripBom(source);

        if (!StartsWithDelimiterLine(source, 0, out var afterOpening))
        {
            return new ParsedDocument(new OrderedDictionary(), source);
        }

        var closingIndex = FindClosingDelimiter(source, afterOpening);
        if (closingIndex is null)
        {
            throw new FrontmatterParseException("Frontmatter block is not terminated by a closing '---' line.");
        }

        var (blockEnd, bodyStart) = closingIndex.Value;
        var yamlBlock = source[afterOpening..blockEnd];
        var body = bodyStart <= source.Length ? source[bodyStart..] : string.Empty;

        return new ParsedDocument(ParseYamlMapping(yamlBlock), body);
    }

    /// <summary>Parses a standalone YAML document (e.g. `mdbase.yaml`) into the JSON data model.</summary>
    public static OrderedDictionary ParseYamlMapping(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new OrderedDictionary();
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (Exception ex) when (ex is not FrontmatterParseException)
        {
            throw new FrontmatterParseException($"YAML parse error: {ex.Message}", ex);
        }

        if (stream.Documents.Count == 0)
        {
            return new OrderedDictionary();
        }

        var root = stream.Documents[0].RootNode;
        if (root is YamlMappingNode mapping)
        {
            return (OrderedDictionary)YamlJsonConverter.Convert(mapping)!;
        }

        if (root is YamlScalarNode { Value: null or "" })
        {
            return new OrderedDictionary();
        }

        throw new FrontmatterParseException("Frontmatter MUST parse to a YAML mapping.");
    }

    private static string StripBom(string source) =>
        source.Length > 0 && source[0] == '\uFEFF' ? source[1..] : source;

    /// <summary>
    /// True when <paramref name="source"/> starts, at <paramref name="offset"/> and with no
    /// preceding whitespace or blank line, with a "---" delimiter line.
    /// </summary>
    private static bool StartsWithDelimiterLine(string source, int offset, out int afterLine)
    {
        afterLine = 0;
        if (!source.AsSpan(offset).StartsWith("---"))
        {
            return false;
        }

        var lineEnd = offset + 3;
        if (lineEnd == source.Length)
        {
            afterLine = lineEnd;
            return true;
        }

        if (source[lineEnd] == '\r' && lineEnd + 1 < source.Length && source[lineEnd + 1] == '\n')
        {
            afterLine = lineEnd + 2;
            return true;
        }

        if (source[lineEnd] == '\n')
        {
            afterLine = lineEnd + 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Scans line-by-line from <paramref name="offset"/> for a line whose trimmed content is
    /// exactly "---". Returns the offset where that line's text begins (block end) and the
    /// offset immediately after its line ending (body start).
    /// </summary>
    private static (int BlockEnd, int BodyStart)? FindClosingDelimiter(string source, int offset)
    {
        var cursor = offset;
        while (cursor <= source.Length)
        {
            var lineEnd = source.IndexOf('\n', cursor);
            var lineEndsAtEof = lineEnd < 0;
            var contentEnd = lineEndsAtEof ? source.Length : lineEnd;
            var content = source[cursor..contentEnd];
            if (content.EndsWith('\r'))
            {
                content = content[..^1];
            }

            if (content == "---")
            {
                var bodyStart = lineEndsAtEof ? source.Length : lineEnd + 1;
                return (cursor, bodyStart);
            }

            if (lineEndsAtEof)
            {
                break;
            }

            cursor = lineEnd + 1;
        }

        return null;
    }
}
