namespace Mdbase.Core.Query;

/// <summary>
/// A structurally invalid query (duplicate `select`/`summary` result names, a named-projection
/// dependency cycle, or a malformed CEL expression) rejected by <see cref="MdbCompiledQuery.Compile"/>
/// before any candidate is evaluated — the `invalid_query` diagnostic code (Ch.16).
/// </summary>
public sealed class MdbInvalidQueryException : Exception
{
    public MdbInvalidQueryException(string message) : base(message)
    {
    }
}

/// <summary>
/// <see cref="MdbQuery.ContextPath"/> did not resolve to a record in the connected collection —
/// the `context_not_found` diagnostic code (Ch.16), thrown by <see cref="MdbCompiledQuery.Execute"/>
/// before any candidate is evaluated.
/// </summary>
public sealed class MdbQueryContextNotFoundException : Exception
{
    public MdbQueryContextNotFoundException(string message) : base(message)
    {
    }
}
