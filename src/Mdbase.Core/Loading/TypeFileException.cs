namespace Mdbase.Core.Loading;

/// <summary>
/// Raised when a candidate type file fails validation/compilation (spec Ch.05 "Type
/// Evaluation Model": "Diagnostics for an invalid definition identify the type-file path and
/// the failing section"). Carries the exact diagnostic code/message to report; the type is
/// excluded from the registry rather than aborting the whole collection load.
/// </summary>
internal sealed class TypeFileException : Exception
{
    public TypeFileException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
