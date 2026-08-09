using System.Collections.Specialized;
using Celly.Values;
using Mdbase.Core.Write;

namespace Mdbase.Core.Cel;

/// <summary>
/// Evaluates one <see cref="MdbLifecycleRule"/>'s optional guard against the current write
/// draft (spec Ch.09 "Guards"). A guard-less rule always fires. A guard that fails to compile
/// is already rejected at type-load (<see cref="Loading.TypeFileLoader"/>); a guard that throws
/// or errors at evaluation time — including a `file` reference that's unavailable during
/// deferred-path `on_create` (#41 point 39) — fails the whole write with
/// `lifecycle_expression_error` before any mutation (#41 point 7).
/// </summary>
internal static class LifecycleGuardEvaluator
{
    public static bool ShouldRun(
        MdbLifecycleRule rule,
        OrderedDictionary draftFields,
        OrderedDictionary? oldRawFrontmatter,
        MdbLifecycleOperation operation,
        MdbFileCel? file,
        string relativePathForDiagnostics)
    {
        if (rule.Guard is null)
        {
            return true;
        }

        if (rule.GuardReferencesFile && file is null)
        {
            throw new MdbWriteException(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "lifecycle_expression_error",
                Message = $"Lifecycle guard '{rule.GuardSource}' for field '{rule.Field}' references 'file', which is unavailable before post-lifecycle path generation.",
                Path = relativePathForDiagnostics,
                Field = rule.Field,
            });
        }

        try
        {
            var activation = LifecycleGuardActivation.Build(
                draftFields, oldRawFrontmatter, operation, file,
                rule.Guard.PresentFields, rule.Guard.FreeIdentifiers, rule.Guard.ReferencedTopLevelFields);
            var result = rule.Guard.Program.Eval(activation);
            if (result.IsError)
            {
                throw new MdbWriteException(new MdbDiagnostic
                {
                    Severity = MdbSeverity.Error,
                    Code = "lifecycle_expression_error",
                    Message = $"Lifecycle guard '{rule.GuardSource}' failed to evaluate for '{relativePathForDiagnostics}': {result}",
                    Path = relativePathForDiagnostics,
                    Field = rule.Field,
                });
            }

            return result is BoolValue { Value: true };
        }
        catch (MdbWriteException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new MdbWriteException(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "lifecycle_expression_error",
                Message = $"Lifecycle guard '{rule.GuardSource}' failed to evaluate for '{relativePathForDiagnostics}': {ex.Message}",
                Path = relativePathForDiagnostics,
                Field = rule.Field,
            });
        }
    }
}
