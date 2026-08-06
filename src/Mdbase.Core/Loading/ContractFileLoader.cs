using System.Collections;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;
using Mdbase.Core.Json;
using Org.Webpki.JsonCanonicalizer;

namespace Mdbase.Core.Loading;

/// <summary>Compiles one parsed <c>mdbase.contract</c> definition into its eager registry form.</summary>
internal static class ContractFileLoader
{
    private const string ContractKind = "mdbase.contract";

    private static readonly System.Text.RegularExpressions.Regex SemVerPattern = new(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    public static bool IsContractCandidate(OrderedDictionary frontmatter) =>
        frontmatter.Contains("kind") && frontmatter["kind"] is string kind && kind == ContractKind;

    public static MdbContract Load(OrderedDictionary frontmatter, string relativeFilePath, string collectionRoot)
    {
        try
        {
            if (frontmatter["id"] is not string id || id.Length == 0 ||
                frontmatter["version"] is not string version || version.Length == 0 ||
                frontmatter["name"] is not string name || name.Length == 0 ||
                frontmatter["contract_type"] is not string contractTypeText)
            {
                throw Invalid(relativeFilePath, "must declare non-empty string id, version, name, and contract_type values");
            }
            if (!SemVerPattern.IsMatch(version) || HasLeadingZeroPrereleaseIdentifier(version))
            {
                throw Invalid(relativeFilePath, $"has non-SemVer version '{version}'");
            }

            var contractType = contractTypeText switch
            {
                "record" => ContractType.Record,
                "event" => ContractType.Event,
                "action" => ContractType.Action,
                _ => throw Invalid(relativeFilePath, $"has unsupported contract_type '{contractTypeText}'"),
            };

            var allowed = contractType switch
            {
                ContractType.Record => new HashSet<string>(StringComparer.Ordinal) { "kind", "id", "version", "name", "description", "contract_type", "record_schema", "binding_schema" },
                ContractType.Event => new HashSet<string>(StringComparer.Ordinal) { "kind", "id", "version", "name", "description", "contract_type", "data_schema", "source_schema" },
                _ => new HashSet<string>(StringComparer.Ordinal) { "kind", "id", "version", "name", "description", "contract_type", "input_schema", "output_schema", "error_schema", "provider_schema", "behavior" },
            };
            foreach (DictionaryEntry entry in frontmatter)
            {
                var key = (string)entry.Key;
                if (!allowed.Contains(key) && !key.StartsWith("x-", StringComparison.Ordinal))
                {
                    throw Invalid(relativeFilePath, $"declares '{key}', which does not belong to contract_type '{contractTypeText}'");
                }
            }

            var schemas = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            JsonSchema? Compile(string key, bool required)
            {
                if (!frontmatter.Contains(key) || frontmatter[key] is null)
                {
                    if (required) throw Invalid(relativeFilePath, $"is missing required '{key}'");
                    return null;
                }
                if (frontmatter[key] is not OrderedDictionary schemaSection)
                    throw Invalid(relativeFilePath, $"has a non-mapping '{key}'");
                var (schema, node) = TypeFileLoader.CompileSchema(schemaSection, relativeFilePath, collectionRoot);
                schemas[key] = node;
                return schema;
            }

            var recordSchema = contractType == ContractType.Record ? Compile("record_schema", true) : null;
            var bindingSchema = contractType == ContractType.Record ? Compile("binding_schema", false) : null;
            var dataSchema = contractType == ContractType.Event ? Compile("data_schema", true) : null;
            var sourceSchema = contractType == ContractType.Event ? Compile("source_schema", false) : null;
            var inputSchema = contractType == ContractType.Action ? Compile("input_schema", true) : null;
            var outputSchema = contractType == ContractType.Action ? Compile("output_schema", false) : null;
            var errorSchema = contractType == ContractType.Action ? Compile("error_schema", false) : null;
            var providerSchema = contractType == ContractType.Action ? Compile("provider_schema", false) : null;
            var behavior = contractType == ContractType.Action ? frontmatter["behavior"] as OrderedDictionary : null;
            if (contractType == ContractType.Action && frontmatter.Contains("behavior") && frontmatter["behavior"] is not OrderedDictionary)
                throw Invalid(relativeFilePath, "has a non-mapping 'behavior'");

            var digestInput = new JsonObject
            {
                ["kind"] = ContractKind,
                ["contract_type"] = contractTypeText,
                ["id"] = id,
                ["version"] = version,
            };
            if (behavior is not null) digestInput["behavior"] = JsonModel.ToJsonNode(behavior);
            foreach (var (key, node) in schemas) digestInput[key] = node.DeepClone();
            var digest = Digest(digestInput);

            return new MdbContract
            {
                Id = id, Version = version, Name = name, Description = frontmatter["description"] as string,
                FilePath = relativeFilePath, ContractType = contractType,
                RecordSchema = recordSchema, BindingSchema = bindingSchema, DataSchema = dataSchema, SourceSchema = sourceSchema,
                InputSchema = inputSchema, OutputSchema = outputSchema, ErrorSchema = errorSchema, ProviderSchema = providerSchema,
                Behavior = behavior, Digest = digest, ResolvedSchemas = schemas,
            };
        }
        catch (ContractFileException) { throw; }
        catch (Exception ex) when (ex is TypeFileException or ArgumentException)
        {
            throw Invalid(relativeFilePath, ex.Message);
        }
    }

    internal static string Digest(JsonNode value)
    {
        var canonical = new JsonCanonicalizer(value.ToJsonString()).GetEncodedUTF8();
        return "sha256:" + Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    private static bool HasLeadingZeroPrereleaseIdentifier(string version)
    {
        var prereleaseStart = version.IndexOf('-');
        if (prereleaseStart < 0) return false;
        return version[(prereleaseStart + 1)..].Split('+')[0].Split('.').Any(identifier =>
            identifier.Length > 1 && identifier[0] == '0' && identifier.All(char.IsAsciiDigit));
    }

    private static ContractFileException Invalid(string path, string message) =>
        new($"Contract file '{path}' {message}.");
}

internal sealed class ContractFileException : Exception
{
    public ContractFileException(string message) : base(message) { }
}
