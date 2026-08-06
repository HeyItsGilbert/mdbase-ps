using System.Collections.Specialized;
using Json.Schema;
using Mdbase.Core.Matching;

namespace Mdbase.Core;

/// <summary>
/// A loaded mdbase type (spec Ch.05): a compiled JSON Schema plus compiled collection
/// semantics. Everything here is eager-compiled at collection-load time (#7/#8) so record
/// matching and validation never re-parse YAML, a glob, or a schema per record.
/// </summary>
public sealed record MdbType
{
    /// <summary>Canonical (as-authored) type name, e.g. "task". Compared case-insensitively for matching/conflicts.</summary>
    public required string Name { get; init; }

    /// <summary>Collection-relative, forward-slash path to the defining type file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Positive integer type-file version, when declared.</summary>
    public int? Version { get; init; }

    /// <summary>Persisted frontmatter object shape validator, compiled once at type-load time.</summary>
    public required JsonSchema Schema { get; init; }

    /// <summary>Compiled `match` predicate; a type with no `match` section never contributes an inferred match.</summary>
    internal CompiledMatch Match { get; init; } = CompiledMatch.None;

    /// <summary>Decomposed `collection.read_defaults` — effective read/query values for fields missing on a record.</summary>
    public IReadOnlyDictionary<string, object?> ReadDefaults { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// The raw `collection` mapping, undecomposed beyond <see cref="ReadDefaults"/>. `links`,
    /// `path`, `unique`, `display`, and `projections` stay raw blobs — their own future specs
    /// (Links, Core Write, Query) own decomposing them.
    /// </summary>
    public OrderedDictionary? CollectionSection { get; init; }

    public string CanonicalName => Name.ToLowerInvariant();
}
