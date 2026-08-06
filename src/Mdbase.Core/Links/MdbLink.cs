namespace Mdbase.Core.Links;

/// <summary>
/// One parsed link occurrence (spec Ch.08 "Link Components"; #9 point 2) — frontmatter-declared
/// or body-discovered, always this one fixed shape regardless of source. <see cref="ResolvedPath"/>
/// and <see cref="IsAmbiguous"/> start unset at parse time and are filled in during phase 3
/// resolution (#9 point 4). An unparsed/malformed target still produces an <see cref="MdbLink"/>
/// with <see cref="ResolvedPath"/> null — resolution failure is data, never a thrown exception.
/// </summary>
public sealed record MdbLink
{
    /// <summary>The exact substring (frontmatter value, or body occurrence) this link was parsed from.</summary>
    public required string Raw { get; init; }

    /// <summary>The link target, with any <c>|alias</c> and <c>#anchor</c> decoration removed.</summary>
    public required string Target { get; init; }

    /// <summary>The display alias, when the link syntax declares one.</summary>
    public string? Alias { get; init; }

    /// <summary>The <c>#anchor</c> fragment, when present.</summary>
    public string? Anchor { get; init; }

    /// <summary>Which of the three link syntaxes produced this link.</summary>
    public required MdbLinkFormat Format { get; init; }

    /// <summary>False only for a collection-root-absolute target (one beginning with <c>/</c>).</summary>
    public required bool IsRelative { get; init; }

    /// <summary>
    /// The collection-relative path this link resolved to, or null when unresolved, ambiguous,
    /// or normalizing outside the collection root. Filled in during phase 3.
    /// </summary>
    public string? ResolvedPath { get; init; }

    /// <summary>True when resolution found more than one candidate and could not deterministically pick one.</summary>
    public bool IsAmbiguous { get; init; }
}
