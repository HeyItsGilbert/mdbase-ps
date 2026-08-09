using Celly.Values;
using Mdbase.Core.Cel;

namespace Mdbase.Core.Query;

/// <summary>
/// A preflight-validated, compiled <see cref="MdbQuery"/> (Ch.11): reusable across repeated
/// <see cref="Execute"/> calls against a live (possibly since-`Refresh`ed) <see cref="MdbCollection"/>
/// without recompiling any CEL expression (#10 point 5).
/// </summary>
public sealed class MdbCompiledQuery
{
    private readonly MdbQuery _query;
    private readonly IReadOnlyList<(string Name, CompiledCelExpression Compiled)> _projections;
    private readonly CompiledCelExpression? _where;
    private readonly IReadOnlyList<(string Name, CompiledCelExpression Compiled)> _select;
    private readonly IReadOnlyDictionary<string, CompiledCelExpression> _summaryFunctions;

    private MdbCompiledQuery(
        MdbQuery query,
        IReadOnlyList<(string Name, CompiledCelExpression Compiled)> projections,
        CompiledCelExpression? where,
        IReadOnlyList<(string Name, CompiledCelExpression Compiled)> select,
        IReadOnlyDictionary<string, CompiledCelExpression> summaryFunctions)
    {
        _query = query;
        _projections = projections;
        _where = where;
        _select = select;
        _summaryFunctions = summaryFunctions;
    }

    /// <summary>
    /// Preflights and compiles <paramref name="query"/>: rejects duplicate `select`/`summary`
    /// result names, a named-projection dependency cycle, an unknown summary function, or any
    /// malformed CEL expression with <see cref="MdbInvalidQueryException"/> before touching a
    /// collection.
    /// </summary>
    public static MdbCompiledQuery Compile(MdbQuery query)
    {
        var selectNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in query.Select)
        {
            if (!selectNames.Add(item.Name))
            {
                throw new MdbInvalidQueryException($"Duplicate select result name '{item.Name}'.");
            }
        }

        var summaryNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var summary in query.Summaries)
        {
            var resultName = summary.ResultName ?? $"{summary.Function}_{summary.Field}";
            if (!summaryNames.Add(resultName))
            {
                throw new MdbInvalidQueryException($"Duplicate summary result name '{resultName}'.");
            }

            if (!QuerySummaryFunctions.Names.Contains(summary.Function) && !query.SummaryFunctions.ContainsKey(summary.Function))
            {
                throw new MdbInvalidQueryException($"Summary '{resultName}' names unknown function '{summary.Function}'.");
            }
        }

        if (selectNames.Overlaps(summaryNames))
        {
            throw new MdbInvalidQueryException("A select result name collides with a summary result name.");
        }

        var compiledProjections = CompileProjectionsInDependencyOrder(query.Projections);
        var where = query.Where is null ? null : CompileOrThrow(CelExpressionContext.Query, query.Where, "where");
        var select = query.Select
            .Select(item => (item.Name, CompileOrThrow(CelExpressionContext.Query, item.Expression, $"select '{item.Name}'")))
            .ToList();
        var summaryFunctions = query.SummaryFunctions.ToDictionary(
            kv => kv.Key,
            kv => CompileOrThrow(CelExpressionContext.Summary, kv.Value, $"summary_functions '{kv.Key}'"));

        return new MdbCompiledQuery(query, compiledProjections, where, select, summaryFunctions);
    }

    /// <summary>
    /// Runs the compiled query against <paramref name="collection"/> (Ch.11 pipeline: types
    /// filter → context resolution → per-candidate projections/where → select → order → group →
    /// summarize → paginate).
    /// </summary>
    public MdbQueryResultSet Execute(MdbCollection collection)
    {
        var diagnostics = new List<MdbDiagnostic>();

        MdbRecord? context = null;
        if (_query.ContextPath is not null)
        {
            var normalized = _query.ContextPath.Replace('\\', '/').TrimStart('/');
            if (!collection.Records.TryGetValue(normalized, out context))
            {
                throw new MdbQueryContextNotFoundException($"Query context '{_query.ContextPath}' does not resolve to a record in this collection.");
            }
        }

        var candidates = _query.Types is { Count: > 0 } types
            ? collection.Records.Values.Where(r => r.MatchedTypes.Any(t => types.Contains(t.Name, StringComparer.OrdinalIgnoreCase)))
            : collection.Records.Values;

        var evaluated = new List<CandidateEvaluation>();
        foreach (var record in candidates.OrderBy(r => r.FileInfo.Path, StringComparer.Ordinal))
        {
            var evaluation = EvaluateCandidate(record, context, collection.RootPath, diagnostics);
            if (evaluation is not null)
            {
                evaluated.Add(evaluation);
            }
        }

        var ordered = ApplyOrder(evaluated, _query.OrderBy, collection.RootPath);
        var totalCount = ordered.Count;

        IReadOnlyList<MdbGroupResult>? groups = null;
        IReadOnlyDictionary<string, object?>? ungroupedSummaries = null;
        if (_query.GroupBy.Count > 0)
        {
            groups = BuildGroups(ordered, collection.RootPath, diagnostics);
        }
        else if (_query.Summaries.Count > 0)
        {
            ungroupedSummaries = ComputeSummaries(ordered, collection.RootPath, diagnostics);
        }

        var offset = Math.Max(_query.Offset ?? 0, 0);
        var page = ordered.Skip(offset);
        if (_query.Limit is { } limit)
        {
            page = page.Take(Math.Max(limit, 0));
        }

        var pageList = page.ToList();
        var hasMore = offset + pageList.Count < totalCount;

        var results = pageList.Select(BuildResult).ToList();

        return new MdbQueryResultSet
        {
            Results = results,
            Meta = new MdbQueryMeta
            {
                TotalCount = totalCount,
                HasMore = hasMore,
                Context = context?.FileInfo.Path,
                Groups = groups,
                Summaries = ungroupedSummaries,
            },
            Diagnostics = diagnostics,
        };
    }

    private MdbQueryResult BuildResult(CandidateEvaluation evaluation)
    {
        var record = evaluation.Record;
        var values = _select.Count == 0
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(evaluation.SelectValues, StringComparer.Ordinal);

        return new MdbQueryResult
        {
            FileInfo = record.FileInfo,
            Frontmatter = _query.FrontmatterMode is MdbFrontmatterMode.Persisted or MdbFrontmatterMode.Both ? record.Frontmatter : null,
            EffectiveFrontmatter = _query.FrontmatterMode is MdbFrontmatterMode.Effective or MdbFrontmatterMode.Both ? record.EffectiveFrontmatter : null,
            Body = _query.IncludeBody ? record.Body : null,
            Values = values,
        };
    }

    private CandidateEvaluation? EvaluateCandidate(MdbRecord record, MdbRecord? context, string collectionRoot, List<MdbDiagnostic> diagnostics)
    {
        var projectionCelValues = new Dictionary<string, CelValue>(StringComparer.Ordinal);
        var projectionNativeValues = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, compiled) in _projections)
        {
            var activation = MdbActivation.ForQuery(record, context, collectionRoot, projectionCelValues, compiled.PresentFields, compiled.FreeIdentifiers, compiled.ReferencedTopLevelFields);
            CelValue result;
            try
            {
                result = compiled.Program.Eval(activation);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add(Diagnostic("projection_error", record.FileInfo.Path, name, ex.Message));
                projectionCelValues[name] = NullValue.Instance;
                projectionNativeValues[name] = null;
                continue;
            }

            if (result.IsError)
            {
                diagnostics.Add(Diagnostic("projection_error", record.FileInfo.Path, name, result.ToString() ?? "evaluation error"));
                projectionCelValues[name] = NullValue.Instance;
                projectionNativeValues[name] = null;
                continue;
            }

            projectionCelValues[name] = result;
            projectionNativeValues[name] = CelValueConversion.ToMdbValue(result);
        }

        if (_where is not null)
        {
            var activation = MdbActivation.ForQuery(record, context, collectionRoot, projectionCelValues, _where.PresentFields, _where.FreeIdentifiers, _where.ReferencedTopLevelFields);
            CelValue result;
            try
            {
                result = _where.Program.Eval(activation);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add(Diagnostic("where_error", record.FileInfo.Path, null, ex.Message));
                return null;
            }

            if (result.IsError)
            {
                diagnostics.Add(Diagnostic("where_error", record.FileInfo.Path, null, result.ToString() ?? "evaluation error"));
                return null;
            }

            if (result is not BoolValue { Value: true })
            {
                return null;
            }
        }

        var selectValues = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, compiled) in _select)
        {
            var activation = MdbActivation.ForQuery(record, context, collectionRoot, projectionCelValues, compiled.PresentFields, compiled.FreeIdentifiers, compiled.ReferencedTopLevelFields);
            CelValue result;
            try
            {
                result = compiled.Program.Eval(activation);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add(Diagnostic("selection_error", record.FileInfo.Path, name, ex.Message));
                selectValues[name] = null;
                continue;
            }

            if (result.IsError)
            {
                diagnostics.Add(Diagnostic("selection_error", record.FileInfo.Path, name, result.ToString() ?? "evaluation error"));
                selectValues[name] = null;
                continue;
            }

            selectValues[name] = CelValueConversion.ToMdbValue(result);
        }

        return new CandidateEvaluation(record, selectValues, projectionNativeValues);
    }

    private List<CandidateEvaluation> ApplyOrder(List<CandidateEvaluation> candidates, IReadOnlyList<MdbSortKey> keys, string collectionRoot)
    {
        IOrderedEnumerable<CandidateEvaluation>? ordered = null;
        var comparer = new NullsLastComparer();
        foreach (var key in keys)
        {
            object? Selector(CandidateEvaluation e) => QueryFieldResolver.Resolve(key.Field, e.Record, collectionRoot, e.SelectValues, e.ProjectionValues);
            ordered = ordered is null
                ? (key.Direction == MdbSortDirection.Descending ? candidates.OrderByDescending(Selector, comparer) : candidates.OrderBy(Selector, comparer))
                : (key.Direction == MdbSortDirection.Descending ? ordered.ThenByDescending(Selector, comparer) : ordered.ThenBy(Selector, comparer));
        }

        ordered = ordered is null
            ? candidates.OrderBy(e => e.Record.FileInfo.Path, StringComparer.Ordinal)
            : ordered.ThenBy(e => e.Record.FileInfo.Path, StringComparer.Ordinal);

        return ordered.ToList();
    }

    private IReadOnlyList<MdbGroupResult> BuildGroups(List<CandidateEvaluation> ordered, string collectionRoot, List<MdbDiagnostic> diagnostics)
    {
        var groups = new List<(IReadOnlyDictionary<string, object?> Key, List<CandidateEvaluation> Members)>();
        foreach (var candidate in ordered)
        {
            var key = _query.GroupBy.ToDictionary(
                k => k.Field,
                k => QueryFieldResolver.Resolve(k.Field, candidate.Record, collectionRoot, candidate.SelectValues, candidate.ProjectionValues));

            var existing = groups.FirstOrDefault(g => KeysEqual(g.Key, key));
            if (existing.Members is not null)
            {
                existing.Members.Add(candidate);
            }
            else
            {
                groups.Add((key, new List<CandidateEvaluation> { candidate }));
            }
        }

        return groups.Select(g => new MdbGroupResult
        {
            Values = g.Key,
            Count = g.Members.Count,
            Summaries = ComputeSummaries(g.Members, collectionRoot, diagnostics),
        }).ToList();
    }

    private static bool KeysEqual(IReadOnlyDictionary<string, object?> left, IReadOnlyDictionary<string, object?> right) =>
        left.Count == right.Count && left.All(kv => right.TryGetValue(kv.Key, out var value) && Json.JsonModel.DeepEquals(kv.Value, value));

    private Dictionary<string, object?> ComputeSummaries(IReadOnlyList<CandidateEvaluation> members, string collectionRoot, List<MdbDiagnostic> diagnostics)
    {
        var results = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var summary in _query.Summaries)
        {
            var resultName = summary.ResultName ?? $"{summary.Function}_{summary.Field}";
            var values = members.Select(m => QueryFieldResolver.Resolve(summary.Field, m.Record, collectionRoot, m.SelectValues, m.ProjectionValues)).ToList();

            if (QuerySummaryFunctions.Names.Contains(summary.Function))
            {
                var (result, error) = QuerySummaryFunctions.Evaluate(summary.Function, values);
                if (error is not null)
                {
                    diagnostics.Add(Diagnostic("summary_incompatible_value", null, resultName, error));
                }

                results[resultName] = result;
                continue;
            }

            var compiled = _summaryFunctions[summary.Function];
            var activation = MdbActivation.ForSummary(values, compiled.FreeIdentifiers);
            try
            {
                var evalResult = compiled.Program.Eval(activation);
                results[resultName] = evalResult.IsError ? null : CelValueConversion.ToMdbValue(evalResult);
                if (evalResult.IsError)
                {
                    diagnostics.Add(Diagnostic("summary_incompatible_value", null, resultName, evalResult.ToString() ?? "evaluation error"));
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add(Diagnostic("summary_incompatible_value", null, resultName, ex.Message));
                results[resultName] = null;
            }
        }

        return results;
    }

    private static MdbDiagnostic Diagnostic(string code, string? path, string? field, string message) => new()
    {
        Severity = MdbSeverity.Warning,
        Code = code,
        Message = message,
        Path = path,
        Field = field,
    };

    private static CompiledCelExpression CompileOrThrow(CelExpressionContext context, string source, string label)
    {
        try
        {
            return context.Compile(source);
        }
        catch (CelCompileException ex)
        {
            throw new MdbInvalidQueryException($"Query {label} is invalid: {ex.Message}");
        }
    }

    private static List<(string Name, CompiledCelExpression Compiled)> CompileProjectionsInDependencyOrder(IReadOnlyDictionary<string, string> sources)
    {
        var compiled = new Dictionary<string, CompiledCelExpression>(StringComparer.Ordinal);
        foreach (var (name, source) in sources)
        {
            compiled[name] = CompileOrThrow(CelExpressionContext.Query, source, $"projections '{name}'");
        }

        var names = compiled.Keys.ToHashSet(StringComparer.Ordinal);
        var ordered = new List<(string Name, CompiledCelExpression Compiled)>();
        var state = new Dictionary<string, int>(StringComparer.Ordinal);

        void Visit(string name, List<string> path)
        {
            if (state.TryGetValue(name, out var s))
            {
                if (s == 0)
                {
                    throw new MdbInvalidQueryException($"Query projections has a dependency cycle: {string.Join(" -> ", path.Append(name))}.");
                }

                return;
            }

            state[name] = 0;
            path.Add(name);
            var dependencies = CelAstScan.SelectFields(compiled[name].Ast, "projection").Intersect(names, StringComparer.Ordinal);
            foreach (var dependency in dependencies)
            {
                if (dependency != name)
                {
                    Visit(dependency, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[name] = 1;
            ordered.Add((name, compiled[name]));
        }

        foreach (var name in names)
        {
            Visit(name, new List<string>());
        }

        return ordered;
    }

    private sealed record CandidateEvaluation(MdbRecord Record, IReadOnlyDictionary<string, object?> SelectValues, IReadOnlyDictionary<string, object?> ProjectionValues);

    /// <summary>
    /// Plain ascending, nulls-last comparer. Sort direction is applied entirely by the caller's
    /// choice of <c>OrderBy</c>/<c>OrderByDescending</c> — LINQ's own reversal then gives
    /// "nulls last ascending, nulls first descending" for free, without this comparer needing
    /// to know the active direction itself.
    /// </summary>
    private sealed class NullsLastComparer : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            return CompareValues(x, y);
        }

        private static int CompareValues(object x, object y)
        {
            if (x is long or double && y is long or double)
            {
                return Convert.ToDouble(x).CompareTo(Convert.ToDouble(y));
            }

            if (x is string xs && y is string ys)
            {
                return string.CompareOrdinal(xs, ys);
            }

            if (x is bool xb && y is bool yb)
            {
                return xb.CompareTo(yb);
            }

            return string.CompareOrdinal(x.ToString(), y.ToString());
        }
    }
}
