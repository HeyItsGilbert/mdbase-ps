using Mdbase.Core.Cel;
using Mdbase.Core.Json;

namespace Mdbase.Core.Write;

/// <summary>Standard lifecycle value providers (spec Ch.09 "Standard Value Providers"; #11 point 2).</summary>
internal enum MdbLifecycleProviderKind
{
    Now,
    Today,
    Uuid,
    Ulid,
    Slugify,
    Copy,
    Literal,
}

/// <summary>
/// One compiled lifecycle assignment (spec Ch.09): a target field, an optional compiled guard,
/// and the provider that produces its value. Grouped by field on <see cref="MdbType"/> so a
/// type's own multiple assignments to the same field execute in declared order (#41 point 8).
/// </summary>
internal sealed record MdbLifecycleRule
{
    public required string Field { get; init; }

    public string? GuardSource { get; init; }

    public CompiledCelExpression? Guard { get; init; }

    /// <summary>True when the guard's AST references the bare `file` identifier (#41 point 39).</summary>
    public bool GuardReferencesFile { get; init; }

    public required MdbLifecycleProviderKind ProviderKind { get; init; }

    public object? ProviderArg { get; init; }
}

/// <summary>
/// Structural equality for the lifecycle composition axis (#34/#41): two field-level rule
/// sequences coalesce only when every rule's normalized guard source, provider kind, and
/// provider args match, in the same order — "same provider with a differing guard still
/// conflicts".
/// </summary>
internal sealed class MdbLifecycleRuleListComparer : IEqualityComparer<IReadOnlyList<MdbLifecycleRule>>
{
    public static readonly MdbLifecycleRuleListComparer Instance = new();

    public bool Equals(IReadOnlyList<MdbLifecycleRule>? x, IReadOnlyList<MdbLifecycleRule>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Count != y.Count)
        {
            return false;
        }

        for (var i = 0; i < x.Count; i++)
        {
            if (!RuleEquals(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(IReadOnlyList<MdbLifecycleRule> obj) => obj.Count;

    private static bool RuleEquals(MdbLifecycleRule a, MdbLifecycleRule b) =>
        string.Equals(a.Field, b.Field, StringComparison.Ordinal)
        && string.Equals(Normalize(a.GuardSource), Normalize(b.GuardSource), StringComparison.Ordinal)
        && a.ProviderKind == b.ProviderKind
        && JsonModel.DeepEquals(a.ProviderArg, b.ProviderArg);

    private static string Normalize(string? source) => source?.Trim() ?? string.Empty;
}
