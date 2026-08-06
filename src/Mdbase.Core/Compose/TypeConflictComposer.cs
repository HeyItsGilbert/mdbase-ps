namespace Mdbase.Core.Compose;

/// <summary>
/// The shared multi-type conflict-composition primitive (#34): given a record's frozen
/// matched-type set, a per-axis key extractor, and a per-axis structural-equality comparer,
/// coalesces identical declared values and reports `type_conflict` for differing ones. A key
/// declared by exactly one type passes through with no comparison. Instantiated for exactly
/// one axis this spec — `read_defaults` — the other three (links, path, projections) wait for
/// the specs that own them.
/// </summary>
internal static class TypeConflictComposer
{
    public static (IReadOnlyDictionary<string, TValue> Coalesced, IReadOnlyList<MdbDiagnostic> Conflicts) Compose<TValue>(
        IReadOnlyList<MdbType> matchedTypes,
        Func<MdbType, IReadOnlyDictionary<string, TValue>> keyExtractor,
        IEqualityComparer<TValue> comparer,
        string recordPath)
    {
        var declarations = new Dictionary<string, List<(MdbType Type, TValue Value)>>();
        foreach (var type in matchedTypes)
        {
            foreach (var (key, value) in keyExtractor(type))
            {
                if (!declarations.TryGetValue(key, out var entries))
                {
                    entries = new List<(MdbType, TValue)>();
                    declarations[key] = entries;
                }

                entries.Add((type, value));
            }
        }

        var coalesced = new Dictionary<string, TValue>();
        var conflicts = new List<MdbDiagnostic>();
        foreach (var (key, entries) in declarations)
        {
            var first = entries[0].Value;
            if (entries.All(e => comparer.Equals(e.Value, first)))
            {
                coalesced[key] = first;
                continue;
            }

            var conflictingTypes = entries.Select(e => e.Type.Name).ToArray();
            conflicts.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "type_conflict",
                Message = $"Field '{key}' has conflicting declared values across matched types: {string.Join(", ", conflictingTypes)}.",
                Path = recordPath,
                Field = key,
                Details = new Dictionary<string, object?> { ["types"] = conflictingTypes },
            });
        }

        return (coalesced, conflicts);
    }
}
