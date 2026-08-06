namespace Mdbase.Core;

/// <summary>Per-field frontmatter state (spec Ch.03 "Missing, Null, And Empty").</summary>
public enum MdbPresentState
{
    /// <summary>Key is not present in raw frontmatter and no effective value was derived for it.</summary>
    Missing,

    /// <summary>Key is present in raw frontmatter with an explicit YAML null.</summary>
    Null,

    /// <summary>Key is present in raw frontmatter with a non-null value.</summary>
    Raw,

    /// <summary>Key was missing in raw frontmatter; its value came from `collection.read_defaults`.</summary>
    Effective,
}

/// <summary>
/// Per-field missing/null/raw/effective state for one <see cref="MdbRecord"/> (spec Ch.03/#10),
/// covering every raw frontmatter key plus every field any matched type declares a read
/// default for (so a conflicted, derived-value-unavailable field is still observable as
/// <see cref="MdbPresentState.Missing"/>, not silently absent from this structure).
/// </summary>
public sealed class MdbPresent
{
    private readonly IReadOnlyDictionary<string, MdbPresentState> _fields;

    internal MdbPresent(IReadOnlyDictionary<string, MdbPresentState> fields)
    {
        _fields = fields;
    }

    public MdbPresentState this[string field] => _fields.TryGetValue(field, out var state) ? state : MdbPresentState.Missing;

    public IReadOnlyDictionary<string, MdbPresentState> Fields => _fields;
}
