using System.Collections.Specialized;

namespace Mdbase.Core;

/// <summary>
/// An immutable, loaded record snapshot (spec Ch.01/03; #7 point 3). Every field is fixed at
/// construction — a later <see cref="MdbCollection.Refresh"/> produces a new instance rather
/// than mutating this one, so a caller can safely hold a reference across a refresh.
/// </summary>
public sealed record MdbRecord
{
    public required MdbFileInfo FileInfo { get; init; }

    /// <summary>The parsed mapping persisted in the Markdown file. Never contains read defaults or other derived data.</summary>
    public required OrderedDictionary Frontmatter { get; init; }

    /// <summary>Raw frontmatter plus `collection.read_defaults` coalesced onto genuinely missing keys.</summary>
    public required OrderedDictionary EffectiveFrontmatter { get; init; }

    /// <summary>Per-field missing/null/raw/effective state.</summary>
    public required MdbPresent Present { get; init; }

    /// <summary>The Markdown content after the closing frontmatter delimiter.</summary>
    public required string Body { get; init; }

    /// <summary>`sha256:` + lowercase hex SHA-256 of the persisted file's exact bytes.</summary>
    public required string Revision { get; init; }

    /// <summary>Matched types in explicit-then-inferred precedence order (spec Ch.05 "Type Membership").</summary>
    public required IReadOnlyList<MdbType> MatchedTypes { get; init; }

    /// <summary>True only when raw frontmatter validates against every matched type's schema (vacuously true when untyped).</summary>
    public required bool IsValid { get; init; }

    /// <summary>Per-matched-type JSON Schema validation failures (spec Ch.06).</summary>
    public required IReadOnlyList<MdbDiagnostic> ValidationDiagnostics { get; init; }

    /// <summary>`type_conflict` diagnostics from composing `collection.read_defaults` across <see cref="MatchedTypes"/> (#34).</summary>
    public required IReadOnlyList<MdbDiagnostic> CompositionDiagnostics { get; init; }
}
