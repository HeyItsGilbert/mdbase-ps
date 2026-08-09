using Celly.Checking;
using Celly.Providers;
using Celly.Stdlib;
using Celly.Types;
using Celly.Values;

namespace Mdbase.Core.Cel;

/// <summary>
/// Host functions every CEL context registers (Ch.10 "Date And Duration" / "File And Link
/// Helpers", base profile only): <c>now()</c>/<c>today()</c> (CEL's own <c>duration(string)</c>
/// standard conversion needs no registration) plus <c>file.inFolder(path)</c>, the one file
/// helper that doesn't depend on Links (#9).
/// </summary>
internal static class CelHostFunctions
{
    /// <summary>
    /// <c>today()</c>'s timezone is UTC — no runtime/operation-context timezone concept exists
    /// yet outside the durable runtime (out of scope for this spec). Documented gap, not a
    /// silently wrong default: a future ticket threads a configured timezone through.
    /// </summary>
    public static readonly IReadOnlyList<FunctionDecl> Declarations = new[]
    {
        new FunctionDecl("now", new[] { new OverloadDecl("now", Array.Empty<CelType>(), CelType.Timestamp, isInstance: false) }),
        new FunctionDecl("today", new[] { new OverloadDecl("today", Array.Empty<CelType>(), CelType.Timestamp, isInstance: false) }),
        new FunctionDecl("inFolder", new[]
        {
            new OverloadDecl("file_inFolder_string", new[] { CelType.Struct("MdbFileCel"), CelType.String }, CelType.Bool, isInstance: true),
        }),
    };

    public static readonly NativeTypeProvider FileTypeProvider = NativeTypeProvider.FromTypes(new[] { typeof(MdbFileCel) });

    public static void Configure(FunctionRegistry registry)
    {
        registry.Register("now", _ => TimestampValue.Of(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 0));
        registry.Register("today", _ =>
        {
            var today = DateTime.UtcNow.Date;
            return TimestampValue.Of(new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeSeconds(), 0);
        });
        registry.Register("inFolder", args =>
        {
            var file = (MdbFileCel)args[0].ToNative()!;
            var folder = ((StringValue)args[1]).Value;
            return BoolValue.Of(file.InFolder(folder));
        });
    }
}
