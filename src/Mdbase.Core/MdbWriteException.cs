namespace Mdbase.Core;

/// <summary>
/// Raised by <see cref="MdbCollection"/>'s write methods (<c>Create</c>/<c>Update</c>/<c>Delete</c>/
/// <c>Rename</c>) on every hard failure — schema validation, <c>concurrent_modification</c>,
/// <c>type_conflict</c>, <c>type_membership_changed</c>, <c>unique_conflict</c>, path errors,
/// <c>lifecycle_expression_error</c>, record-not-found (#41). Carries a single structured
/// <see cref="MdbDiagnostic"/> — one uniform, catchable failure shape instead of parsing
/// return-value variants. <see cref="MdbCollection.ExecuteBatch"/> never throws this for a
/// per-operation failure; that's what its envelope list is for.
/// </summary>
public sealed class MdbWriteException : Exception
{
    public MdbWriteException(MdbDiagnostic diagnostic) : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public MdbDiagnostic Diagnostic { get; }
}
