using System.Text.RegularExpressions;

namespace Mdbase.Core.Links;

/// <summary>
/// The shared wikilink/Markdown-link/bare-path grammar (spec Ch.08 "Link Values" and "Link
/// Components"), used both for a whole-string frontmatter link-field value
/// (<see cref="ParseValue"/>) and for one already-located body occurrence
/// (<see cref="BuildWikilink"/>/<see cref="BuildMarkdownLink"/>).
/// </summary>
internal static class LinkGrammar
{
    private static readonly Regex WikilinkPattern =
        new(@"^!?\[\[(?<inner>[^\[\]]+)\]\]$", RegexOptions.Compiled);

    private static readonly Regex MarkdownLinkPattern =
        new(@"^!?\[(?<alias>[^\[\]]*)\]\((?<target>[^()]*)\)$", RegexOptions.Compiled);

    /// <summary>Classifies and parses one whole frontmatter link-field string value.</summary>
    public static MdbLink ParseValue(string raw)
    {
        var trimmed = raw.Trim();

        var wiki = WikilinkPattern.Match(trimmed);
        if (wiki.Success)
        {
            return BuildWikilink(raw, wiki.Groups["inner"].Value);
        }

        var markdown = MarkdownLinkPattern.Match(trimmed);
        if (markdown.Success)
        {
            return BuildMarkdownLink(raw, markdown.Groups["alias"].Value, markdown.Groups["target"].Value);
        }

        return BuildPath(raw, trimmed);
    }

    /// <summary><paramref name="inner"/> is the content between <c>[[</c> and <c>]]</c> (alias/anchor undecomposed).</summary>
    public static MdbLink BuildWikilink(string raw, string inner)
    {
        var target = inner;
        string? alias = null;

        var pipeIndex = inner.IndexOf('|');
        if (pipeIndex >= 0)
        {
            target = inner[..pipeIndex];
            alias = inner[(pipeIndex + 1)..].Trim();
        }

        var (bareTarget, anchor) = SplitAnchor(target.Trim());

        return new MdbLink
        {
            Raw = raw,
            Target = bareTarget,
            Alias = string.IsNullOrEmpty(alias) ? null : alias,
            Anchor = anchor,
            Format = MdbLinkFormat.Wikilink,
            IsRelative = !bareTarget.StartsWith('/'),
        };
    }

    public static MdbLink BuildMarkdownLink(string raw, string alias, string targetRaw)
    {
        var (bareTarget, anchor) = SplitAnchor(targetRaw);

        return new MdbLink
        {
            Raw = raw,
            Target = bareTarget,
            Alias = string.IsNullOrEmpty(alias) ? null : alias,
            Anchor = anchor,
            Format = MdbLinkFormat.Markdown,
            IsRelative = !bareTarget.StartsWith('/'),
        };
    }

    private static MdbLink BuildPath(string raw, string trimmed)
    {
        var (bareTarget, anchor) = SplitAnchor(trimmed);

        return new MdbLink
        {
            Raw = raw,
            Target = bareTarget,
            Alias = null,
            Anchor = anchor,
            Format = MdbLinkFormat.Path,
            IsRelative = !bareTarget.StartsWith('/'),
        };
    }

    private static (string Target, string? Anchor) SplitAnchor(string target)
    {
        var hashIndex = target.IndexOf('#');
        if (hashIndex < 0)
        {
            return (target, null);
        }

        var anchor = target[(hashIndex + 1)..];
        return (target[..hashIndex], anchor.Length == 0 ? null : anchor);
    }
}
