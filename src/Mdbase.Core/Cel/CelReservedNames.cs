namespace Mdbase.Core.Cel;

/// <summary>
/// The fixed vocabulary of CEL system names (spec Ch.10 "Reserved Names"): every name any
/// evaluation context binds, across every context. A bare identifier prescan (#17 mechanism A)
/// must never declare one of these <c>dyn</c> as a frontmatter-field shadow, even when the
/// *current* context doesn't itself bind it — that's exactly what makes referencing an
/// out-of-context reserved name (e.g. `this` in match context) a compile-time "undeclared
/// reference" diagnostic instead of a silently-accepted dynamic field.
/// </summary>
internal static class CelReservedNames
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "record", "raw", "present", "file", "note", "projection", "this", "values",
        "old", "operation", "event", "workflow", "trigger", "steps", "vars", "item",
    };

    /// <summary>
    /// Celly's own standard type identifiers (<c>Checking.StandardDecls.CreateIdents()</c>),
    /// pre-declared as root variables before any <see cref="Celly.CelEnvSettings.Declarations"/>
    /// is applied. A frontmatter field literally named `int` or `list` must never be
    /// Dyn-redeclared over these (#17's second caveat).
    /// </summary>
    public static readonly IReadOnlySet<string> StandardTypeIdentifiers = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "int", "uint", "double", "string", "bytes", "list", "map", "null_type", "type",
        "google.protobuf.Timestamp", "google.protobuf.Duration",
    };

    /// <summary>The full exclusion set for the bare-identifier Dyn-declare prescan.</summary>
    public static readonly IReadOnlySet<string> ExcludedFromPrescan = All.Concat(StandardTypeIdentifiers).ToHashSet(StringComparer.Ordinal);
}
