using System.Collections.Specialized;
using Json.Schema;
using Mdbase.Core.Compose;
using Mdbase.Core.Links;
using Mdbase.Core.Matching;
using Mdbase.Core.Write;

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

    /// <summary>Decomposed `collection.links` — per-field link rules (spec Ch.07 "Links"), keyed by field reference.</summary>
    public IReadOnlyDictionary<string, LinkFieldRule> LinkRules { get; init; } = new Dictionary<string, LinkFieldRule>();

    /// <summary>
    /// The raw `collection` mapping, undecomposed beyond <see cref="ReadDefaults"/>,
    /// <see cref="LinkRules"/>, and <see cref="ProjectionSources"/>. `path`/`unique`/`display`
    /// stay raw blobs — their own future specs (Core Write) own decomposing them.
    /// </summary>
    public OrderedDictionary? CollectionSection { get; init; }

    /// <summary>
    /// Declared `collection.projections` source text, keyed by projection/target-field name
    /// (#34's coalesce-vs-conflict axis: identical source text coalesces across matched types,
    /// differing text produces `type_conflict`).
    /// </summary>
    public IReadOnlyDictionary<string, string> ProjectionSources { get; init; } = new Dictionary<string, string>();

    /// <summary>This type's own `collection.projections`, compiled and dependency-ordered (a projection may reference an earlier one in this same list by bare name).</summary>
    internal IReadOnlyList<MdbCompiledProjection> CompiledProjections { get; init; } = Array.Empty<MdbCompiledProjection>();

    /// <summary>Resolved data-contract claims compiled during type loading.</summary>
    public IReadOnlyList<MdbTypeImplementation> Implements { get; init; } = Array.Empty<MdbTypeImplementation>();

    /// <summary>
    /// Decomposed `lifecycle.on_create`/`on_update` (spec Ch.09; #41), keyed by target field —
    /// each value is this type's own ordered rule sequence for that field (#41 point 8: later
    /// assignments to the same field execute in declared order). `on_delete`/`on_rename` are
    /// compiled at type-load for forward compatibility (#41 point 11) but no write pipeline
    /// executes them, so they are not retained here.
    /// </summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>> LifecycleOnCreate { get; init; } = new Dictionary<string, IReadOnlyList<MdbLifecycleRule>>();

    internal IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>> LifecycleOnUpdate { get; init; } = new Dictionary<string, IReadOnlyList<MdbLifecycleRule>>();

    /// <summary>Decomposed `collection.path.pattern` (spec Ch.07 "Path Policy"), compiled once at type-load time.</summary>
    internal MdbPathPattern? PathPattern { get; init; }

    /// <summary>Decomposed `collection.unique` (spec Ch.07 "Cross-File Uniqueness") — additive per declaring type, never composed via <see cref="TypeConflictComposer"/>.</summary>
    internal IReadOnlyList<MdbUniqueRule> Unique { get; init; } = Array.Empty<MdbUniqueRule>();

    public string CanonicalName => Name.ToLowerInvariant();
}
