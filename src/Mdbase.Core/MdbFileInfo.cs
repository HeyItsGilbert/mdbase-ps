namespace Mdbase.Core;

/// <summary>
/// File-metadata envelope for a record (spec Ch.01/03): a collection-relative,
/// forward-slash-normalized path plus its derived name/folder — one clear home
/// for path-derived data instead of ad hoc string fields smeared across <see cref="MdbRecord"/>.
/// </summary>
public sealed record MdbFileInfo
{
    /// <summary>Collection-relative, forward-slash path, e.g. "tasks/fix-login.md".</summary>
    public required string Path { get; init; }

    /// <summary>Basename with extension, e.g. "fix-login.md".</summary>
    public string Name => Path.Length == 0
        ? string.Empty
        : Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>Collection-relative containing folder, e.g. "tasks". Empty string at the collection root.</summary>
    public string Directory
    {
        get
        {
            var slash = Path.LastIndexOf('/');
            return slash < 0 ? string.Empty : Path[..slash];
        }
    }
}
