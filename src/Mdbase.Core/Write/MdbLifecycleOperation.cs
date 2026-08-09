namespace Mdbase.Core.Write;

/// <summary>
/// Minimal lifecycle operation metadata (spec Ch.09/Ch.10; #11 point 4): surfaced to the CEL
/// guard as a string (<c>operation.kind == "update"</c>) — no speculative fields.
/// </summary>
internal readonly record struct MdbLifecycleOperation(string Kind)
{
    public static readonly MdbLifecycleOperation Create = new("create");

    public static readonly MdbLifecycleOperation Update = new("update");
}
