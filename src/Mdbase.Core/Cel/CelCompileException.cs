namespace Mdbase.Core.Cel;

/// <summary>A stored CEL expression failed to parse or type-check (#4/#17's "must compile during preflight" requirement).</summary>
internal sealed class CelCompileException : Exception
{
    public CelCompileException(string message) : base(message)
    {
    }
}
