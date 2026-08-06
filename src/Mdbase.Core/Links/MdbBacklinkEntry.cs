namespace Mdbase.Core.Links;

/// <summary>
/// One entry in <see cref="MdbCollection"/>'s phase-3 backward (backlink) index (#9 point 3):
/// one resolved, non-ambiguous outgoing link, keyed elsewhere by its <see cref="MdbLink.ResolvedPath"/>.
/// </summary>
public sealed record MdbBacklinkEntry
{
    /// <summary>Collection-relative, forward-slash path of the referring record.</summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The declaring frontmatter field reference (e.g. <c>assignee</c>, <c>/relations[2]</c>) for a
    /// frontmatter-origin link, or null for a body-origin link.
    /// </summary>
    public string? FieldPath { get; init; }

    /// <summary>The resolved link itself.</summary>
    public required MdbLink Link { get; init; }
}
