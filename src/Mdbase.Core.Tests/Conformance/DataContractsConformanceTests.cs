using System.Collections;
using System.Collections.Specialized;
using Mdbase.Core.Tests.Fixtures;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Tests.Conformance;

/// <summary>
/// Runs the vendored v0.3 <c>data_contracts</c> fixtures through MdbCollection's public
/// contract-registry, implementation lookup, and contract-view seams.
/// </summary>
public class DataContractsConformanceTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "vendored", "v0.3", "data-contracts", "data-contracts.yaml");
    private static readonly string SourcesPath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "vendored", "v0.3", "data-contracts", "sources");

    public static IEnumerable<object[]> Cases() =>
        ConformanceCase.LoadAll(File.ReadAllText(FixturePath)).Select(testCase => new object[] { testCase });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fixture_case_matches_expectation(ConformanceCase testCase)
    {
        using var fixture = new TempCollection(testCase.ConfigYaml);
        WriteSetup(fixture, testCase);

        switch (testCase.Operation)
        {
            case "data_contract_implementation_validate":
                AssertImplementationValidation(fixture, testCase);
                break;
            case "data_contract_digest":
                AssertContractDigest(fixture, testCase);
                break;
            case "data_contract_implementation_digest":
                AssertImplementationDigest(fixture, testCase);
                break;
            case "data_contract_registry_validate":
                AssertContractRegistry(fixture, testCase);
                break;
            case "get_data_contracts":
                AssertImplementations(fixture, testCase);
                break;
            case "get_contract_view":
                AssertContractView(fixture, testCase);
                break;
            default:
                Assert.Fail($"Conformance case '{testCase}' uses unsupported operation '{testCase.Operation}'.");
                break;
        }
    }

    private static void WriteSetup(TempCollection fixture, ConformanceCase testCase)
    {
        foreach (var (name, content) in testCase.Contracts)
        {
            fixture.WriteFile($"_contracts/{name}", content);
        }

        foreach (var (name, content) in testCase.Types)
        {
            fixture.WriteFile($"_types/{name}", content);
        }

        foreach (var (path, content) in testCase.Files)
        {
            fixture.WriteFile(path, content);
        }
    }

    private static void AssertImplementationValidation(TempCollection fixture, ConformanceCase testCase)
    {
        fixture.WriteFile("_contracts/contract.md", Source((string)testCase.Input["contract"]!));
        var typeSource = Source((string)testCase.Input["type"]!);
        fixture.WriteFile("_types/type.md", typeSource);
        if (testCase.Input["record"] is string recordSource)
        {
            var typeName = (string)FrontmatterParser.Parse(typeSource).Frontmatter["name"]!;
            fixture.WriteFile("record.md", $"---\ntype: {typeName}\n{Source(recordSource)}---\n");
        }

        var collection = MdbCollection.Connect(fixture.RootPath);
        var errors = collection.Diagnostics.Select(diagnostic => $"{diagnostic.Code.Replace('_', ' ')}: {diagnostic.Message}").ToList();
        var valid = collection.Types.Count == 1 && collection.Contracts.Count == 1;
        if (valid && testCase.Input["record"] is string)
        {
            var type = Assert.Single(collection.Types).Value;
            var record = Assert.Single(collection.Records).Value;
            if (!record.MatchedTypes.Contains(type))
            {
                valid = false;
                errors.Add("record does not match the implementing type");
            }
            else if (type.Implements.Count != 1)
            {
                valid = false;
                errors.Add($"expected exactly one implementation; found {type.Implements.Count}");
            }
            else
            {
                var implementation = type.Implements[0];
                valid = collection.GetContractView(record, type, implementation.ContractId, implementation.ContractVersion).Diagnostic is null;
                if (!valid) errors.Add("record failed contract-view validation");
            }
        }
        else if (valid)
        {
            valid = Assert.Single(collection.Types).Value.Implements.Count == 1;
            if (!valid) errors.Add("expected exactly one implementation");
        }

        Assert.Equal((bool)testCase.Expect["valid"]!, valid);
        AssertExpectedError(testCase, errors);
    }

    private static void AssertContractDigest(TempCollection fixture, ConformanceCase testCase)
    {
        fixture.WriteFile("_contracts/contract.md", Source((string)testCase.Input["contract"]!));

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal((string)testCase.Expect["digest"]!, Assert.Single(collection.Contracts).Value.Digest);
    }

    private static void AssertImplementationDigest(TempCollection fixture, ConformanceCase testCase)
    {
        fixture.WriteFile("_contracts/contract.md", Source((string)testCase.Input["contract"]!));
        fixture.WriteFile("_types/type.md", Source((string)testCase.Input["type"]!));

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal((string)testCase.Expect["digest"]!, Assert.Single(Assert.Single(collection.Types).Value.Implements).ImplementationDigest);
    }

    private static void AssertContractRegistry(TempCollection fixture, ConformanceCase testCase)
    {
        foreach (var (index, source) in ((object?[])testCase.Input["paths"]!).Cast<string>().Select((source, index) => (index, source)))
        {
            fixture.WriteFile($"_contracts/{index}.md", Source(source));
        }

        var collection = MdbCollection.Connect(fixture.RootPath);
        var errors = collection.Diagnostics.Select(diagnostic => $"{diagnostic.Code.Replace('_', ' ')}: {diagnostic.Message}");

        Assert.Equal((bool)testCase.Expect["valid"]!, !collection.Diagnostics.Any(diagnostic => diagnostic.Code == "data_contract_conflict"));
        AssertExpectedError(testCase, errors);
    }

    private static void AssertExpectedError(ConformanceCase testCase, IEnumerable<string> errors)
    {
        if (testCase.Expect["error_contains"] is string expected)
        {
            Assert.Contains(errors, error => error.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertImplementations(TempCollection fixture, ConformanceCase testCase)
    {
        var collection = MdbCollection.Connect(fixture.RootPath);
        var expected = ((object?[])testCase.Expect["implementations"]!)
            .Cast<OrderedDictionary>()
            .Select(implementation => (string)implementation["type"]!);

        Assert.Equal(expected, collection.GetImplementations((string)testCase.Input["contract"]!, (string)testCase.Input["version"]!).Select(type => type.Name));
    }

    private static void AssertContractView(TempCollection fixture, ConformanceCase testCase)
    {
        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records[(string)testCase.Input["path"]!];
        var type = Assert.Single(record.MatchedTypes);
        var view = collection.GetContractView(record, type, (string)testCase.Input["contract"]!, (string)testCase.Input["version"]!);

        Assert.Null(view.Diagnostic);
        foreach (DictionaryEntry expected in (OrderedDictionary)testCase.Expect["view"]!)
        {
            Assert.Equal(expected.Value, view.View[(string)expected.Key]!.GetValue<string>());
        }
    }

    private static string Source(string sourcePath) => File.ReadAllText(Path.Combine(SourcesPath, Path.GetFileName(sourcePath)));
}
