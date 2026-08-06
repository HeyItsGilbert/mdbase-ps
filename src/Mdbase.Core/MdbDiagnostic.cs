namespace Mdbase.Core;

/// <summary>
/// Severity of an <see cref="MdbDiagnostic"/>, per spec Ch.16 Canonical Diagnostics.
/// </summary>
public enum MdbSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// The canonical mdbase diagnostic shape (spec Ch.16 "Canonical Diagnostics"):
/// every diagnostic carries a required <see cref="Severity"/>, <see cref="Code"/>, and
/// <see cref="Message"/>, plus optional locating fields.
/// </summary>
public sealed record MdbDiagnostic
{
    /// <summary>Required.</summary>
    public required MdbSeverity Severity { get; init; }

    /// <summary>Required. A stable machine-readable code, e.g. "type_conflict", "schema_required".</summary>
    public required string Code { get; init; }

    /// <summary>Required. Human-readable context.</summary>
    public required string Message { get; init; }

    /// <summary>Collection-relative, forward-slash record or type-file path this diagnostic concerns.</summary>
    public string? Path { get; init; }

    /// <summary>JSON Pointer or an explicitly identified frontmatter selector.</summary>
    public string? Field { get; init; }

    /// <summary>The type name this diagnostic concerns, when it concerns exactly one type.</summary>
    public string? Type { get; init; }

    /// <summary>Schema location (e.g. a schema `$id` plus JSON Pointer) when available.</summary>
    public string? SchemaLocation { get; init; }

    /// <summary>Extra structured detail, e.g. the full set of conflicting type names.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}
