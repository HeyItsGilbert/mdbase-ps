using System.Collections.Specialized;
using Mdbase.Core.Cel;
using Celly.Values;

namespace Mdbase.Core.Compose;

/// <summary>
/// Evaluates one matched type's <see cref="MdbType.CompiledProjections"/> chain for one record
/// (Ch.07 "Projections"), in that type's own dependency-resolved order. A projection whose
/// target field already has a raw value is skipped — never evaluated, never an error — so a
/// later projection in the same chain referencing it by bare name still sees the raw value
/// (ordinary top-level name resolution, #10's decision text). A per-record evaluation failure
/// appends a `projection_error` diagnostic and leaves that field unproduced (stays `Missing`).
/// </summary>
internal static class ProjectionEvaluator
{
    public static (IReadOnlyDictionary<string, object?> Values, IReadOnlyList<MdbDiagnostic> Diagnostics) Evaluate(
        MdbType type, OrderedDictionary rawFrontmatter, MdbFileCel file, string relativePath)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var diagnostics = new List<MdbDiagnostic>();

        foreach (var projection in type.CompiledProjections)
        {
            if (rawFrontmatter.Contains(projection.Name))
            {
                // Already present on raw frontmatter: not evaluated, not an error — a computed
                // projection must never silently overwrite an author-supplied value.
                continue;
            }

            try
            {
                var activation = MdbActivation.ForMatch(
                    rawFrontmatter, file, projection.Compiled.PresentFields, projection.Compiled.FreeIdentifiers, projection.Compiled.ReferencedTopLevelFields, values);
                var result = projection.Compiled.Program.Eval(activation);
                if (result.IsError)
                {
                    diagnostics.Add(Error(type, projection, relativePath, result.ToString() ?? "evaluation error"));
                    continue;
                }

                values[projection.Name] = CelValueConversion.ToMdbValue(result);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add(Error(type, projection, relativePath, ex.Message));
            }
        }

        return (values, diagnostics);
    }

    private static MdbDiagnostic Error(MdbType type, MdbCompiledProjection projection, string relativePath, string detail) => new()
    {
        Severity = MdbSeverity.Warning,
        Code = "projection_error",
        Message = $"Projection '{projection.Name}' ('{projection.Source}') on type '{type.Name}' failed to evaluate for '{relativePath}': {detail}",
        Path = relativePath,
        Field = projection.Name,
        Type = type.Name,
    };
}
