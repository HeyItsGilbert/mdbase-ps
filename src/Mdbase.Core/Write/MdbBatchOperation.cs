using System.Collections.Specialized;

namespace Mdbase.Core.Write;

/// <summary>Discriminates one <see cref="MdbBatchOperation"/> (spec Ch.12 "Batch").</summary>
public enum MdbBatchOperationKind
{
    Create,
    Update,
    Delete,
    Rename,
}

/// <summary>
/// One ordered create/update/delete/rename descriptor for <see cref="MdbCollection.ExecuteBatch"/>
/// (spec Ch.12 "Batch"; #41 point 33). Construct via the static factory methods.
/// </summary>
public sealed record MdbBatchOperation
{
    public required MdbBatchOperationKind Kind { get; init; }

    /// <summary>Target path (Update/Delete/Rename source) or an explicit Create target.</summary>
    public string? Path { get; init; }

    /// <summary>Rename destination.</summary>
    public string? NewPath { get; init; }

    /// <summary>Create input frontmatter.</summary>
    public OrderedDictionary? Frontmatter { get; init; }

    /// <summary>Create input body, or an Update body override (mutually exclusive with <see cref="Document"/>).</summary>
    public string? Body { get; init; }

    /// <summary>Create's explicit type list.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Update's set/null patch (mutually exclusive with <see cref="Document"/>).</summary>
    public OrderedDictionary? Patch { get; init; }

    /// <summary>Update's explicit remove-key list (mutually exclusive with <see cref="Document"/>).</summary>
    public IReadOnlyList<string>? Remove { get; init; }

    /// <summary>Update's complete replacement Markdown source (mutually exclusive with <see cref="Patch"/>/<see cref="Remove"/>/<see cref="Body"/>).</summary>
    public string? Document { get; init; }

    public string? IfRevision { get; init; }

    public static MdbBatchOperation Create(OrderedDictionary frontmatter, string? body = null, IReadOnlyList<string>? types = null, string? path = null) =>
        new() { Kind = MdbBatchOperationKind.Create, Frontmatter = frontmatter, Body = body, Types = types, Path = path };

    public static MdbBatchOperation Update(
        string path, OrderedDictionary? patch = null, IReadOnlyList<string>? remove = null, string? body = null, string? document = null, string? ifRevision = null) =>
        new() { Kind = MdbBatchOperationKind.Update, Path = path, Patch = patch, Remove = remove, Body = body, Document = document, IfRevision = ifRevision };

    public static MdbBatchOperation Delete(string path, string? ifRevision = null) =>
        new() { Kind = MdbBatchOperationKind.Delete, Path = path, IfRevision = ifRevision };

    public static MdbBatchOperation Rename(string path, string newPath, string? ifRevision = null) =>
        new() { Kind = MdbBatchOperationKind.Rename, Path = path, NewPath = newPath, IfRevision = ifRevision };
}

/// <summary>One per-operation outcome from <see cref="MdbCollection.ExecuteBatch"/> — the spec's operation-result envelope shape (Ch.12 "Operation Result Envelope").</summary>
public sealed record MdbBatchOperationResult
{
    public required bool Valid { get; init; }

    public string? Path { get; init; }

    /// <summary>The resulting record for a successful Create/Update/Rename; null for a successful Delete or any failure.</summary>
    public MdbRecord? Result { get; init; }

    public required IReadOnlyList<MdbDiagnostic> Diagnostics { get; init; }
}
