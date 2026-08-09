using Celly;
using Celly.Ast;
using Celly.Checking;
using Celly.Common;
using Celly.Extensions;
using Celly.Providers;
using Celly.Types;

namespace Mdbase.Core.Cel;

/// <summary>
/// A compiled stored expression plus the field names it statically references via
/// <c>present.raw</c>/<c>present.record</c> (#10's presence contract) and via
/// <c>record</c>/<c>raw</c>/<c>note</c>/<c>this.record</c>/<c>this.raw</c>/<c>this.note</c>
/// (so a select of a genuinely missing field resolves to CEL null, never a Celly "no such key"
/// runtime error — the same guarantee bare top-level names already get).
/// </summary>
internal sealed record CompiledCelExpression(
    CelProgram Program,
    IReadOnlySet<string> PresentFields,
    IReadOnlySet<string> FreeIdentifiers,
    Expr Ast,
    IReadOnlySet<string> ReferencedTopLevelFields);

/// <summary>
/// One fixed Ch.10 evaluation context (match/query/summary): its reserved-name declarations,
/// wired once, reused to compile every stored expression that context ever sees. Per #17's
/// mechanism A, only the *free-identifier* declaration set varies per expression — declared
/// fresh on every <see cref="Compile"/> call via <see cref="AstTools.ReferencedVariables"/>.
/// </summary>
internal sealed class CelExpressionContext
{
    private readonly IReadOnlyList<VariableDecl> _baseDeclarations;
    private readonly ITypeProvider _typeProvider;
    private readonly ITypeAdapter _adapter;

    /// <summary>Every non-protobuf extension library (strings/math/optionals/encoders/bindings/block/two-var-comprehensions/network) — enabled everywhere, matching the rich standard-library surface Ch.10's worked examples assume.</summary>
    private static readonly IReadOnlyList<CelLibrary> ExtensionLibraries = new CelLibrary[]
    {
        OptionalsLibrary.Instance, StringsLibrary.Instance, MathLibrary.Instance, EncodersLibrary.Instance,
        BindingsLibrary.Instance, BlockLibrary.Instance, TwoVarComprehensionsLibrary.Instance, NetworkLibrary.Instance,
    };

    private CelExpressionContext(IReadOnlyList<VariableDecl> baseDeclarations, ITypeProvider typeProvider, ITypeAdapter adapter)
    {
        _baseDeclarations = baseDeclarations;
        _typeProvider = typeProvider;
        _adapter = adapter;
    }

    /// <summary>Inferred `match.expr` / `collection.projections` (Ch.07): raw-frontmatter-only, no query-local namespace.</summary>
    public static readonly CelExpressionContext Match = new(
        new VariableDecl[]
        {
            new("record", CelType.MapDyn),
            new("raw", CelType.MapDyn),
            new("note", CelType.MapDyn),
            new("present", CelType.MapDyn),
            new("file", CelType.Struct("MdbFileCel")),
        },
        CelHostFunctions.FileTypeProvider,
        CelHostFunctions.FileTypeProvider);

    /// <summary>Query filter/select/order/group/named-projection expressions (Ch.11): effective top-level fields plus `projection.<name>`/`this`.</summary>
    public static readonly CelExpressionContext Query = new(
        new VariableDecl[]
        {
            new("record", CelType.MapDyn),
            new("raw", CelType.MapDyn),
            new("note", CelType.MapDyn),
            new("present", CelType.MapDyn),
            new("file", CelType.Struct("MdbFileCel")),
            new("projection", CelType.MapDyn),
            new("this", CelType.MapDyn),
        },
        CelHostFunctions.FileTypeProvider,
        CelHostFunctions.FileTypeProvider);

    /// <summary>Custom `summary_functions` (Ch.11 "Grouping And Summaries"): the one reserved name is `values`, the ordered per-group column.</summary>
    public static readonly CelExpressionContext Summary = new(
        new VariableDecl[] { new("values", CelType.ListDyn) },
        new EmptyTypeProvider(),
        NativeTypeAdapter.Instance);

    public CompiledCelExpression Compile(string source)
    {
        var baseEnv = CelEnv.Create(new CelEnvSettings());
        var parsed = baseEnv.Parse(source);
        if (parsed.HasErrors)
        {
            throw new CelCompileException(FormatIssues(source, parsed.Issues));
        }

        var freeNames = AstTools.ReferencedVariables(parsed.Ast!.Expr)
            .Where(name => !CelReservedNames.ExcludedFromPrescan.Contains(name))
            .ToHashSet(StringComparer.Ordinal);
        var free = freeNames.Select(name => new VariableDecl(name, CelType.Dyn));

        var declarations = _baseDeclarations.Concat(free).ToList();
        var settings = new CelEnvSettings
        {
            Declarations = declarations,
            FunctionDeclarations = CelHostFunctions.Declarations,
            ConfigureFunctions = CelHostFunctions.Configure,
            TypeProvider = _typeProvider,
            Adapter = _adapter,
            Libraries = ExtensionLibraries,
        };

        var env = CelEnv.Create(settings);
        var check = env.Check(parsed.Ast!);
        if (check.HasErrors)
        {
            throw new CelCompileException(FormatIssues(source, check.Issues));
        }

        var presentFields = CelAstScan.NestedSelectFields(parsed.Ast!.Expr, "present", "raw")
            .Concat(CelAstScan.NestedSelectFields(parsed.Ast!.Expr, "present", "record"))
            .ToHashSet(StringComparer.Ordinal);

        var referencedTopLevelFields = CelAstScan.SelectFields(parsed.Ast!.Expr, "record")
            .Concat(CelAstScan.SelectFields(parsed.Ast!.Expr, "raw"))
            .Concat(CelAstScan.SelectFields(parsed.Ast!.Expr, "note"))
            .Concat(CelAstScan.NestedSelectFields(parsed.Ast!.Expr, "this", "record"))
            .Concat(CelAstScan.NestedSelectFields(parsed.Ast!.Expr, "this", "raw"))
            .Concat(CelAstScan.NestedSelectFields(parsed.Ast!.Expr, "this", "note"))
            .ToHashSet(StringComparer.Ordinal);

        return new CompiledCelExpression(env.Program(parsed.Ast!), presentFields, freeNames, parsed.Ast!.Expr, referencedTopLevelFields);
    }

    private static string FormatIssues(string source, IReadOnlyList<CelIssue> issues) =>
        $"CEL expression '{source}' failed to compile: {string.Join("; ", issues.Select(i => i.Message))}";
}
