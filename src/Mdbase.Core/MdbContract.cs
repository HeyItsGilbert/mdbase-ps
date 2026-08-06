using System.Collections.Specialized;
using Json.Schema;

namespace Mdbase.Core;

/// <summary>The subject kind of an <see cref="MdbContract"/>.</summary>
public enum ContractType
{
    Record,
    Event,
    Action,
}

/// <summary>
/// A fully compiled, collection-local data contract. Subject-specific members are populated only
/// for the matching <see cref="ContractType"/>.
/// </summary>
public sealed record MdbContract
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required string FilePath { get; init; }
    public required ContractType ContractType { get; init; }
    public JsonSchema? RecordSchema { get; init; }
    public JsonSchema? BindingSchema { get; init; }
    public JsonSchema? DataSchema { get; init; }
    public JsonSchema? SourceSchema { get; init; }
    public JsonSchema? InputSchema { get; init; }
    public JsonSchema? OutputSchema { get; init; }
    public JsonSchema? ErrorSchema { get; init; }
    public JsonSchema? ProviderSchema { get; init; }
    public OrderedDictionary? Behavior { get; init; }
    public required string Digest { get; init; }

    internal IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode> ResolvedSchemas { get; init; } = new Dictionary<string, System.Text.Json.Nodes.JsonNode>();
}

/// <summary>A type's validated claim to implement one exact contract version.</summary>
public sealed record MdbTypeImplementation
{
    public required string ContractId { get; init; }
    public required string ContractVersion { get; init; }
    public required string ContractDigest { get; init; }
    public required IReadOnlyDictionary<string, string> Fields { get; init; }
    public OrderedDictionary? Binding { get; init; }
    public required string ImplementationDigest { get; init; }
}

/// <summary>The normalized view and optional record-validation finding from a contract projection.</summary>
public sealed record MdbContractView(System.Text.Json.Nodes.JsonNode View, MdbDiagnostic? Diagnostic);
