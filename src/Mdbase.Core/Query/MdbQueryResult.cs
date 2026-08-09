using System.Collections.Specialized;

namespace Mdbase.Core.Query;

/// <summary>One result row (Ch.11 "Result Envelope"): frontmatter member(s) chosen by <see cref="MdbQuery.FrontmatterMode"/>, plus the requested `select` outputs.</summary>
public sealed record MdbQueryResult
{
    public required MdbFileInfo FileInfo { get; init; }

    /// <summary>Populated when <see cref="MdbQuery.FrontmatterMode"/> is <see cref="MdbFrontmatterMode.Persisted"/> or <see cref="MdbFrontmatterMode.Both"/>.</summary>
    public OrderedDictionary? Frontmatter { get; init; }

    /// <summary>Populated when <see cref="MdbQuery.FrontmatterMode"/> is <see cref="MdbFrontmatterMode.Effective"/> or <see cref="MdbFrontmatterMode.Both"/>.</summary>
    public OrderedDictionary? EffectiveFrontmatter { get; init; }

    /// <summary>Populated when <see cref="MdbQuery.IncludeBody"/> is set.</summary>
    public string? Body { get; init; }

    /// <summary>The requested `select` outputs; empty when the query declared no `select`.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>();
}

/// <summary>One `group_by` bucket (Ch.11 "Grouping And Summaries").</summary>
public sealed record MdbGroupResult
{
    /// <summary>The group's key tuple, keyed by each <see cref="MdbQuery.GroupBy"/> field.</summary>
    public required IReadOnlyDictionary<string, object?> Values { get; init; }

    public required int Count { get; init; }

    /// <summary>Keyed by each summary's result name.</summary>
    public required IReadOnlyDictionary<string, object?> Summaries { get; init; }
}

/// <summary>The canonical Ch.11 result-set metadata.</summary>
public sealed record MdbQueryMeta
{
    /// <summary>The complete filtered-and-ordered match count, before `limit`/`offset` (Ch.11 "Pagination").</summary>
    public required int TotalCount { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>The bound `context.this`'s `file.path`; <c>null</c> when the query declared no `context`.</summary>
    public string? Context { get; init; }

    /// <summary>Present only when `group_by`/`summaries` were requested.</summary>
    public IReadOnlyList<MdbGroupResult>? Groups { get; init; }

    /// <summary>Present only when the query requested no `group_by` but did request `summaries` — one implicit whole-set group's worth of summary values.</summary>
    public IReadOnlyDictionary<string, object?>? Summaries { get; init; }
}

/// <summary>The canonical Ch.11 query result envelope.</summary>
public sealed record MdbQueryResultSet
{
    public required IReadOnlyList<MdbQueryResult> Results { get; init; }

    public required MdbQueryMeta Meta { get; init; }

    public required IReadOnlyList<MdbDiagnostic> Diagnostics { get; init; }
}
