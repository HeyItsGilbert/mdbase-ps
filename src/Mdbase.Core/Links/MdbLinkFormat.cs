namespace Mdbase.Core.Links;

/// <summary>The three link syntaxes mdbase recognizes (spec Ch.08 "Link Values").</summary>
public enum MdbLinkFormat
{
    /// <summary><c>[[target]]</c>, optionally with <c>|alias</c> and/or <c>#anchor</c>.</summary>
    Wikilink,

    /// <summary><c>[alias](target)</c>, optionally with a <c>#anchor</c> on the target.</summary>
    Markdown,

    /// <summary>A bare path string with no wikilink/Markdown-link decoration.</summary>
    Path,
}
