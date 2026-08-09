using System.Collections;
using System.Collections.Specialized;
using Celly.Interpreter;
using Celly.Values;

namespace Mdbase.Core.Cel;

/// <summary>
/// One <see cref="IActivation"/> for one context evaluation (#10 point 2): a lazily-populated
/// name-to-thunk map for the context's fixed reserved names, backed by a bare-identifier
/// fallback pool for every other (Dyn-declared) free name the compiler's prescan accepted.
/// <see cref="TryFind"/> only ever invokes a thunk for a name Celly's evaluator actually asks
/// for — an expression that never references `present`/`this`/etc. never pays to build them.
/// A name that resolves to nothing genuinely present still evaluates as CEL null, never a Celly
/// "no such key" runtime error (Ch.10 "Missing And Null": "hosts MUST make missing record
/// fields evaluate to null").
/// </summary>
internal sealed class MdbActivation : IActivation
{
    private readonly IReadOnlyDictionary<string, Func<CelValue>> _reserved;
    private readonly OrderedDictionary? _topLevel;
    private readonly IReadOnlyDictionary<string, object?>? _topLevelOverrides;
    private readonly IReadOnlySet<string> _freeIdentifiers;

    private MdbActivation(
        IReadOnlyDictionary<string, Func<CelValue>> reserved,
        OrderedDictionary? topLevel,
        IReadOnlyDictionary<string, object?>? topLevelOverrides,
        IReadOnlySet<string> freeIdentifiers)
    {
        _reserved = reserved;
        _topLevel = topLevel;
        _topLevelOverrides = topLevelOverrides;
        _freeIdentifiers = freeIdentifiers;
    }

    /// <summary>
    /// Resolves only the context's fixed reserved names plus this expression's own
    /// compile-time free-identifier set (<see cref="CompiledCelExpression.FreeIdentifiers"/>).
    /// Any other name — notably a longer dotted candidate the planner probes for container
    /// (`C.name`) resolution, e.g. `record.status` when evaluating `record.status` — MUST fall
    /// through (return <c>false</c>) so Celly resolves it as an ordinary field select on the
    /// `record` value instead of treating the whole dotted string as a hijacked identifier.
    /// Only within that resolved set does a genuinely-missing bare name become CEL null rather
    /// than an error (Ch.10 "Missing And Null").
    /// </summary>
    public bool TryFind(string name, out CelValue value)
    {
        if (_reserved.TryGetValue(name, out var thunk))
        {
            value = thunk();
            return true;
        }

        if (!_freeIdentifiers.Contains(name))
        {
            value = NullValue.Instance;
            return false;
        }

        if (_topLevelOverrides is not null && _topLevelOverrides.TryGetValue(name, out var overrideValue))
        {
            value = CelValueConversion.ToCelValue(overrideValue);
            return true;
        }

        value = _topLevel is not null && _topLevel.Contains(name)
            ? CelValueConversion.ToCelValue(_topLevel[name])
            : NullValue.Instance;
        return true;
    }

    /// <summary>
    /// Matching context (Ch.07 "CEL Matching"/"Projections"): `record`/`raw`/`note` all alias
    /// the same raw object, `present.record` is identical to `present.raw` (no effective
    /// frontmatter exists yet during matching/projection composition).
    /// <paramref name="resolvedProjections"/> carries already-evaluated collection projections
    /// from earlier in this type's own dependency-resolved chain, bound as ordinary top-level
    /// names — the same mechanism a bare frontmatter field uses (#10's decision text).
    /// </summary>
    public static MdbActivation ForMatch(
        OrderedDictionary rawFrontmatter,
        MdbFileCel file,
        IReadOnlySet<string> presentFields,
        IReadOnlySet<string> freeIdentifiers,
        IReadOnlySet<string> referencedTopLevelFields,
        IReadOnlyDictionary<string, object?>? resolvedProjections = null)
    {
        var rawValue = new Lazy<CelValue>(() => BuildFieldMap(rawFrontmatter, referencedTopLevelFields));
        var presence = new Lazy<CelValue>(() => BuildPresenceMap(BuildRawPresence(rawFrontmatter), presentFields));
        var reserved = new Dictionary<string, Func<CelValue>>(StringComparer.Ordinal)
        {
            ["record"] = () => rawValue.Value,
            ["raw"] = () => rawValue.Value,
            ["note"] = () => rawValue.Value,
            ["present"] = () => presence.Value,
            ["file"] = () => CelHostFunctions.FileTypeProvider.NativeToValue(file),
        };

        return new MdbActivation(reserved, rawFrontmatter, resolvedProjections, freeIdentifiers);
    }

    /// <summary>
    /// Query/projection context (Ch.11 "Query Context"): effective top-level fields, `record`
    /// (effective)/`raw` (persisted)/`note` (alias for `record`, effective), `present` (the
    /// record's real four-state <see cref="MdbPresent"/>), `projection.&lt;name&gt;`
    /// (already-evaluated named query projections), and `this` (the bound context record's own
    /// namespace, or CEL null when unbound — "mirroring the candidate query namespaces").
    /// </summary>
    public static MdbActivation ForQuery(
        MdbRecord record,
        MdbRecord? context,
        string collectionRoot,
        IReadOnlyDictionary<string, CelValue> projectionValues,
        IReadOnlySet<string> presentFields,
        IReadOnlySet<string> freeIdentifiers,
        IReadOnlySet<string> referencedTopLevelFields)
    {
        var effective = new Lazy<CelValue>(() => BuildFieldMap(record.EffectiveFrontmatter, referencedTopLevelFields));
        var raw = new Lazy<CelValue>(() => BuildFieldMap(record.Frontmatter, referencedTopLevelFields));
        var presence = new Lazy<CelValue>(() => BuildPresenceMap(RecordPresence(record.Present), presentFields));

        var reserved = new Dictionary<string, Func<CelValue>>(StringComparer.Ordinal)
        {
            ["record"] = () => effective.Value,
            ["raw"] = () => raw.Value,
            ["note"] = () => effective.Value,
            ["present"] = () => presence.Value,
            ["file"] = () => CelHostFunctions.FileTypeProvider.NativeToValue(MdbFileCel.Build(collectionRoot, record.FileInfo.Path, record.Body)),
            ["projection"] = () => MapValue.Build(projectionValues.Select(kv => new KeyValuePair<CelValue, CelValue>(StringValue.Of(kv.Key), kv.Value))),
            ["this"] = () => context is null ? NullValue.Instance : BuildContextNamespace(context, collectionRoot, referencedTopLevelFields),
        };

        return new MdbActivation(reserved, record.EffectiveFrontmatter, null, freeIdentifiers);
    }

    /// <summary>Custom `summary_functions` context (Ch.11 "Grouping And Summaries"): the sole binding is `values`, the ordered per-group column.</summary>
    public static MdbActivation ForSummary(IReadOnlyList<object?> values, IReadOnlySet<string> freeIdentifiers)
    {
        var reserved = new Dictionary<string, Func<CelValue>>(StringComparer.Ordinal)
        {
            ["values"] = () => ListValue.Of(values.Select(CelValueConversion.ToCelValue).ToArray()),
        };

        return new MdbActivation(reserved, null, null, freeIdentifiers);
    }

    /// <summary>
    /// `this`'s namespace (Ch.11 "Query Context"): `this.&lt;field&gt;`/`this.record.&lt;field&gt;`/
    /// `this.note.&lt;field&gt;` expose effective context values, `this.raw.&lt;field&gt;` exposes
    /// persisted frontmatter, `this.present.*` exposes presence, `this.file` exposes context-file
    /// metadata. A context field colliding with a reserved sub-member name (`record`/`note`/
    /// `raw`/`present`/`file`) stays reachable only via `this.record.&lt;field&gt;`/`this.raw.&lt;field&gt;`.
    /// </summary>
    private static CelValue BuildContextNamespace(MdbRecord context, string collectionRoot, IReadOnlySet<string> referencedTopLevelFields)
    {
        var file = CelHostFunctions.FileTypeProvider.NativeToValue(MdbFileCel.Build(collectionRoot, context.FileInfo.Path, context.Body));
        var effective = BuildFieldMap(context.EffectiveFrontmatter, referencedTopLevelFields);
        var raw = BuildFieldMap(context.Frontmatter, referencedTopLevelFields);
        var presence = BuildPresenceMap(RecordPresence(context.Present), context.Present.Fields.Keys.ToHashSet(StringComparer.Ordinal));

        var entries = new List<KeyValuePair<CelValue, CelValue>>
        {
            new(StringValue.Of("record"), effective),
            new(StringValue.Of("raw"), raw),
            new(StringValue.Of("note"), effective),
            new(StringValue.Of("present"), presence),
            new(StringValue.Of("file"), file),
        };

        foreach (DictionaryEntry entry in context.EffectiveFrontmatter)
        {
            var key = (string)entry.Key;
            if (CelReservedNames.All.Contains(key))
            {
                continue;
            }

            entries.Add(new KeyValuePair<CelValue, CelValue>(StringValue.Of(key), CelValueConversion.ToCelValue(entry.Value)));
        }

        return MapValue.Build(entries);
    }

    /// <summary>
    /// Builds a `record`/`raw`/`note` (or `this.record`/`this.raw`/`this.note`) map: every real
    /// frontmatter entry, plus an explicit CEL null for any field the compiled expression
    /// statically selects (<see cref="CompiledCelExpression.ReferencedTopLevelFields"/>) but
    /// which the frontmatter doesn't actually have — otherwise selecting a genuinely missing
    /// field errors ("no such key") instead of resolving to null (Ch.10 "Missing And Null").
    /// </summary>
    private static CelValue BuildFieldMap(OrderedDictionary source, IReadOnlySet<string> referencedFields)
    {
        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        foreach (DictionaryEntry entry in source)
        {
            entries.Add(new KeyValuePair<CelValue, CelValue>(StringValue.Of((string)entry.Key), CelValueConversion.ToCelValue(entry.Value)));
        }

        foreach (var field in referencedFields)
        {
            if (!source.Contains(field))
            {
                entries.Add(new KeyValuePair<CelValue, CelValue>(StringValue.Of(field), NullValue.Instance));
            }
        }

        return MapValue.Build(entries);
    }

    private static Func<string, (bool RawOrNull, bool NotMissing)> BuildRawPresence(OrderedDictionary raw) => key =>
        raw.Contains(key) ? (true, true) : (false, false);

    private static Func<string, (bool RawOrNull, bool NotMissing)> RecordPresence(MdbPresent present) => key =>
    {
        var state = present[key];
        return (state is MdbPresentState.Raw or MdbPresentState.Null, state is not MdbPresentState.Missing);
    };

    /// <summary>Builds the `present.raw`/`present.record` map over exactly the field names the compiled expression statically references (#10's "always resolve, never error" requirement).</summary>
    private static CelValue BuildPresenceMap(Func<string, (bool RawOrNull, bool NotMissing)> lookup, IReadOnlySet<string> presentFields)
    {
        var raw = new Dictionary<string, bool>(StringComparer.Ordinal);
        var record = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var field in presentFields)
        {
            var (rawOrNull, notMissing) = lookup(field);
            raw[field] = rawOrNull;
            record[field] = notMissing;
        }

        var rawMap = MapValue.Build(raw.Select(kv => new KeyValuePair<CelValue, CelValue>(StringValue.Of(kv.Key), BoolValue.Of(kv.Value))));
        var recordMap = MapValue.Build(record.Select(kv => new KeyValuePair<CelValue, CelValue>(StringValue.Of(kv.Key), BoolValue.Of(kv.Value))));
        return MapValue.Build(new[]
        {
            new KeyValuePair<CelValue, CelValue>(StringValue.Of("raw"), rawMap),
            new KeyValuePair<CelValue, CelValue>(StringValue.Of("record"), recordMap),
        });
    }
}
