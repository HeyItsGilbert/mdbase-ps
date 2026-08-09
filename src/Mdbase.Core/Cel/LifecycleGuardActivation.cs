using System.Collections;
using System.Collections.Specialized;
using Celly.Interpreter;
using Celly.Values;
using Mdbase.Core.Write;

namespace Mdbase.Core.Cel;

/// <summary>
/// A minimal, purpose-built <see cref="IActivation"/> for lifecycle guard evaluation (spec
/// Ch.09/Ch.10 "Lifecycle Context"; #41's own decision text) — deliberately distinct from
/// <see cref="MdbActivation"/>, which stays scoped to match/query/summary contexts and is not
/// extended for this spec's guard-only Celly usage.
/// </summary>
internal sealed class LifecycleGuardActivation : IActivation
{
    private readonly IReadOnlyDictionary<string, Func<CelValue>> _reserved;
    private readonly OrderedDictionary _draft;
    private readonly IReadOnlySet<string> _freeIdentifiers;

    private LifecycleGuardActivation(
        IReadOnlyDictionary<string, Func<CelValue>> reserved, OrderedDictionary draft, IReadOnlySet<string> freeIdentifiers)
    {
        _reserved = reserved;
        _draft = draft;
        _freeIdentifiers = freeIdentifiers;
    }

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

        value = _draft.Contains(name) ? CelValueConversion.ToCelValue(_draft[name]) : NullValue.Instance;
        return true;
    }

    public static LifecycleGuardActivation Build(
        OrderedDictionary draftFields,
        OrderedDictionary? oldRawFrontmatter,
        MdbLifecycleOperation operation,
        MdbFileCel? file,
        IReadOnlySet<string> presentFields,
        IReadOnlySet<string> freeIdentifiers,
        IReadOnlySet<string> referencedTopLevelFields)
    {
        var draftValue = new Lazy<CelValue>(() => BuildFieldMap(draftFields, referencedTopLevelFields));
        var oldValue = new Lazy<CelValue>(() => BuildFieldMap(oldRawFrontmatter ?? new OrderedDictionary(), referencedTopLevelFields));
        var presence = new Lazy<CelValue>(() => BuildPresenceMap(draftFields, presentFields));

        var reserved = new Dictionary<string, Func<CelValue>>(StringComparer.Ordinal)
        {
            ["record"] = () => draftValue.Value,
            ["raw"] = () => draftValue.Value,
            ["old"] = () => oldValue.Value,
            ["present"] = () => presence.Value,
            ["operation"] = () => MapValue.Build(new[]
            {
                new KeyValuePair<CelValue, CelValue>(StringValue.Of("kind"), StringValue.Of(operation.Kind)),
            }),
            ["file"] = () => file is null ? NullValue.Instance : CelHostFunctions.FileTypeProvider.NativeToValue(file),
        };

        return new LifecycleGuardActivation(reserved, draftFields, freeIdentifiers);
    }

    /// <summary>
    /// Builds a `record`/`raw`/`old` map: every current entry, plus an explicit CEL null for
    /// any field the compiled guard statically selects but which the source doesn't actually
    /// have — a genuinely missing field must resolve to null, never a Celly "no such key" error
    /// (Ch.10 "Missing And Null").
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

    /// <summary>Draft-only presence (spec "current write draft" has no separate effective layer): `present.raw` and `present.record` are identical — true iff the key exists in the draft, regardless of null.</summary>
    private static CelValue BuildPresenceMap(OrderedDictionary draft, IReadOnlySet<string> presentFields)
    {
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var field in presentFields)
        {
            values[field] = draft.Contains(field);
        }

        var map = MapValue.Build(values.Select(kv => new KeyValuePair<CelValue, CelValue>(StringValue.Of(kv.Key), BoolValue.Of(kv.Value))));
        return MapValue.Build(new[]
        {
            new KeyValuePair<CelValue, CelValue>(StringValue.Of("raw"), map),
            new KeyValuePair<CelValue, CelValue>(StringValue.Of("record"), map),
        });
    }
}
