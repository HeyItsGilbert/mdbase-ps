using System.Text.RegularExpressions;

namespace Mdbase.Core.Write;

/// <summary>Comparison-set scope for one <see cref="MdbUniqueRule"/> (spec Ch.07 "Cross-File Uniqueness").</summary>
internal enum MdbUniqueScope
{
    Collection,
    Type,
    PathGlob,
}

/// <summary>
/// One decomposed `collection.unique` entry (spec Ch.07): additive and evaluated independently
/// per declaring type — never run through <see cref="Compose.TypeConflictComposer"/> (spec
/// Ch.05 "uniqueness rules are additive and are each evaluated in the type that declared them").
/// </summary>
internal sealed record MdbUniqueRule
{
    public required string Field { get; init; }

    public required MdbUniqueScope Scope { get; init; }

    public string? PathGlob { get; init; }

    /// <summary>Compiled once at type-load time, reusing the existing glob component rather than a second implementation.</summary>
    public Regex? CompiledPathGlob { get; init; }
}
