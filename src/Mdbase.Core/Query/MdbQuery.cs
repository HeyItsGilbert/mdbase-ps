namespace Mdbase.Core.Query;

/// <summary>Which frontmatter member(s) serialize into each <see cref="MdbQueryResult"/> (Ch.11 "Result Envelope") — a purely presentational choice; filtering/ordering/grouping/summaries always use effective values regardless of this setting.</summary>
public enum MdbFrontmatterMode
{
    Effective,
    Persisted,
    Both,
}

/// <summary>Sort direction for an <see cref="MdbSortKey"/>.</summary>
public enum MdbSortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// One `select` output (Ch.11 "Selection"): either a bare field-reference string (<paramref name="Name"/> == <paramref name="Expression"/>, e.g. `"title"`) or a named CEL expression (`{name: expr}`).
/// </summary>
public sealed record MdbSelectItem(string Name, string Expression);

/// <summary>
/// One `order_by`/`group_by` key (Ch.11 "Ordering"/"Grouping"): a field reference resolved
/// against the candidate's effective fields, `file.*`, a named query projection, or a named
/// `select` output (in that resolution order) — not an arbitrary CEL expression.
/// </summary>
public sealed record MdbSortKey(string Field, MdbSortDirection Direction = MdbSortDirection.Ascending);

/// <summary>
/// One `summaries` request (Ch.11 "Grouping And Summaries"): <paramref name="Field"/> names the
/// per-candidate value column (resolved the same way as an <see cref="MdbSortKey"/>),
/// <paramref name="Function"/> is one of the nine built-in identifiers or a
/// <see cref="MdbQuery.SummaryFunctions"/> custom name, <paramref name="ResultName"/> defaults
/// to <c>"&lt;function&gt;_&lt;field&gt;"</c> when omitted.
/// </summary>
public sealed record MdbSummaryRequest(string Field, string Function, string? ResultName = null);

/// <summary>
/// An immutable query input (spec Ch.11), constructed directly by a .NET caller — no YAML/
/// `query.schema.json` parsing in this spec (saved views stay out of scope).
/// </summary>
public sealed record MdbQuery
{
    /// <summary>OR-filter by matched-type membership; every record is a candidate when omitted/empty.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Collection-relative path resolved once into `context.this`; <c>null</c> leaves `this` unbound.</summary>
    public string? ContextPath { get; init; }

    /// <summary>Named query projections (Ch.11), name → CEL source; evaluated in dependency order before `where`.</summary>
    public IReadOnlyDictionary<string, string> Projections { get; init; } = new Dictionary<string, string>();

    /// <summary>The `where` CEL predicate; <c>null</c> admits every candidate.</summary>
    public string? Where { get; init; }

    /// <summary>`select` outputs, computed for every candidate that passes `where`.</summary>
    public IReadOnlyList<MdbSelectItem> Select { get; init; } = Array.Empty<MdbSelectItem>();

    public IReadOnlyList<MdbSortKey> OrderBy { get; init; } = Array.Empty<MdbSortKey>();

    public IReadOnlyList<MdbSortKey> GroupBy { get; init; } = Array.Empty<MdbSortKey>();

    /// <summary>Custom `summary_functions` (Ch.11), name → CEL source receiving the reserved `values` list.</summary>
    public IReadOnlyDictionary<string, string> SummaryFunctions { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<MdbSummaryRequest> Summaries { get; init; } = Array.Empty<MdbSummaryRequest>();

    public int? Limit { get; init; }

    public int? Offset { get; init; }

    public bool IncludeBody { get; init; }

    public MdbFrontmatterMode FrontmatterMode { get; init; } = MdbFrontmatterMode.Effective;
}
