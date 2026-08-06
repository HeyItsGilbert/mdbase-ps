using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mdbase.Core.Links;

/// <summary>One record's body-extracted, unresolved link/embed/tag occurrences (#9 point 5).</summary>
internal sealed record BodyOccurrences(
    IReadOnlyList<MdbLink> Links,
    IReadOnlyList<MdbLink> Embeds,
    IReadOnlyList<string> Tags);

/// <summary>
/// Markdig-based body extractor (spec Ch.08 "Body Links"/"Tags"; #9 point 5): walks a record's
/// <c>Body</c> as a CommonMark AST once, so fenced code blocks (no parsed <c>Inline</c>) and
/// inline code spans (<see cref="CodeInline"/>, never descended into) are structurally excluded
/// rather than pattern-matched around. Markdown links/images use Markdig's native
/// <see cref="LinkInline"/>; wikilinks/embeds/tags are not native CommonMark constructs, so they
/// are pattern-matched against the reconstructed plain text of each leaf block's inline content
/// (code spans and link subtrees already excluded from that reconstruction).
/// </summary>
internal static class LinkParser
{
    // Body occurrences frame the alias/target exactly like a frontmatter value; only the
    // outer brackets are located here, `inner` is the same grammar `LinkGrammar` decomposes.
    private static readonly Regex WikilinkPattern =
        new(@"(?<embed>!)?\[\[(?<inner>[^\[\]]+)\]\]", RegexOptions.Compiled);

    // Ch.08 "Tags": begins at start of line or after whitespace; a URL fragment (`#` directly
    // preceded by a non-whitespace character) is never a match.
    private static readonly Regex TagPattern =
        new(@"(?:^|(?<=\s))#(?<tag>[\p{L}\p{N}_/-]+)", RegexOptions.Compiled);

    public static MdbLink ParseFrontmatterValue(string raw) => LinkGrammar.ParseValue(raw);

    public static BodyOccurrences ExtractBody(string body)
    {
        var document = Markdig.Markdown.Parse(body);
        var links = new List<MdbLink>();
        var embeds = new List<MdbLink>();
        var tags = new List<string>();

        WalkBlock(document, links, embeds, tags);

        return new BodyOccurrences(links, embeds, tags);
    }

    private static void WalkBlock(Block block, List<MdbLink> links, List<MdbLink> embeds, List<string> tags)
    {
        switch (block)
        {
            // A fenced/indented code block is a LeafBlock with no parsed Inline — it is
            // structurally skipped simply by never being a ContainerBlock and having a null
            // Inline, with no special-casing required.
            case LeafBlock { Inline: { } inline }:
                WalkLeafInlines(inline, links, embeds, tags);
                break;

            case ContainerBlock container:
                foreach (var child in container)
                {
                    WalkBlock(child, links, embeds, tags);
                }

                break;
        }
    }

    private static void WalkLeafInlines(ContainerInline root, List<MdbLink> links, List<MdbLink> embeds, List<string> tags)
    {
        var text = new StringBuilder();
        CollectPlainTextAndStructuralLinks(root, text, links, embeds);
        ScanPlainText(text.ToString(), links, embeds, tags);
    }

    /// <summary>
    /// Recursively reconstructs one leaf block's plain-text content for wikilink/tag scanning,
    /// while structurally extracting native Markdig <see cref="LinkInline"/> occurrences
    /// (Markdown links/embeds) along the way. Inline code spans contribute no text at all.
    /// </summary>
    private static void CollectPlainTextAndStructuralLinks(
        ContainerInline container, StringBuilder text, List<MdbLink> links, List<MdbLink> embeds)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;

                case LineBreakInline:
                    text.Append('\n');
                    break;

                case CodeInline:
                    // Inline code spans are excluded from link/tag extraction entirely.
                    break;

                case LinkInline link:
                    var alias = ExtractPlainText(link);
                    var raw = (link.IsImage ? "![" : "[") + alias + "](" + (link.Url ?? string.Empty) + ")";
                    var mdLink = LinkGrammar.BuildMarkdownLink(raw, alias, link.Url ?? string.Empty);
                    (link.IsImage ? embeds : links).Add(mdLink);
                    break;

                case ContainerInline nested:
                    CollectPlainTextAndStructuralLinks(nested, text, links, embeds);
                    break;
            }
        }
    }

    private static string ExtractPlainText(ContainerInline container)
    {
        var buffer = new StringBuilder();
        AppendPlainText(container, buffer);
        return buffer.ToString();
    }

    private static void AppendPlainText(ContainerInline container, StringBuilder buffer)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    buffer.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    buffer.Append(code.Content);
                    break;
                case ContainerInline nested:
                    AppendPlainText(nested, buffer);
                    break;
            }
        }
    }

    private static void ScanPlainText(string text, List<MdbLink> links, List<MdbLink> embeds, List<string> tags)
    {
        var blanked = text.ToCharArray();

        foreach (Match match in WikilinkPattern.Matches(text))
        {
            var isEmbed = match.Groups["embed"].Success;
            var wikilink = LinkGrammar.BuildWikilink(match.Value, match.Groups["inner"].Value);
            (isEmbed ? embeds : links).Add(wikilink);

            for (var i = match.Index; i < match.Index + match.Length; i++)
            {
                blanked[i] = ' ';
            }
        }

        foreach (Match match in TagPattern.Matches(new string(blanked)))
        {
            tags.Add(match.Groups["tag"].Value);
        }
    }
}
