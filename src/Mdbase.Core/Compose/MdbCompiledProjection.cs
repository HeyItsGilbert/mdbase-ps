using Mdbase.Core.Cel;

namespace Mdbase.Core.Compose;

/// <summary>
/// One compiled `collection.projections` entry (Ch.07 "Projections"), in this type's own
/// dependency-resolved evaluation order — a projection referencing another projection by bare
/// name (already-resolved earlier in this same list) creates the dependency edge that ordering
/// satisfies (#10's decision text).
/// </summary>
internal sealed record MdbCompiledProjection(string Name, string Source, CompiledCelExpression Compiled);
