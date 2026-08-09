using System.Collections;
using Mdbase.Core.Query;
using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests.Conformance;

/// <summary>
/// Runs the vendored v0.3 <c>cel</c> fixture cases that exercise Mdbase.Core's public
/// collection-query and type-membership seams. See <c>VENDORED.md</c> for excluded operations.
/// </summary>
public class CelConformanceTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "vendored", "v0.3", "cel", "cel-profile.yaml");

    public static IEnumerable<object[]> Cases() =>
        ConformanceCase.LoadAll(File.ReadAllText(FixturePath))
            .Where(testCase => testCase.Operation is "query" or "get_types")
            .Select(testCase => new object[] { testCase });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fixture_case_matches_expectation(ConformanceCase testCase)
    {
        using var fixture = new TempCollection(testCase.ConfigYaml);
        WriteSetup(fixture, testCase);
        var collection = MdbCollection.Connect(fixture.RootPath);

        switch (testCase.Operation)
        {
            case "query":
                AssertQuery(collection, testCase);
                break;
            case "get_types":
                AssertTypes(collection, testCase);
                break;
            default:
                Assert.Fail($"Conformance case '{testCase}' uses unsupported operation '{testCase.Operation}'.");
                break;
        }
    }

    private static void WriteSetup(TempCollection fixture, ConformanceCase testCase)
    {
        foreach (var (name, content) in testCase.Types)
        {
            fixture.WriteFile($"_types/{name}", content);
        }

        foreach (var (path, content) in testCase.Files)
        {
            fixture.WriteFile(path, content);
        }
    }

    private static void AssertQuery(MdbCollection collection, ConformanceCase testCase)
    {
        var input = testCase.Input;
        var query = new MdbQuery
        {
            Types = input["types"] is object?[] types ? types.Cast<string>().ToArray() : null,
            Where = input["where"] as string,
            OrderBy = SortKeys(input["order_by"] as object?[]),
            IncludeBody = input["include_body"] as bool? ?? false,
        };

        var result = MdbCompiledQuery.Compile(query).Execute(collection);
        var expectedPaths = ((object?[])testCase.Expect["results"]!)
            .Cast<IDictionary>()
            .Select(expected => (string)expected["path"]!)
            .ToArray();

        Assert.Equal((bool)testCase.Expect["valid"]!, result.Diagnostics.Count == 0);
        Assert.Equal(expectedPaths, result.Results.Select(row => row.FileInfo.Path));
        if (testCase.Expect["body_returned"] is bool bodyReturned)
        {
            Assert.All(result.Results, row => Assert.Equal(bodyReturned, row.Body is not null));
        }
    }

    private static IReadOnlyList<MdbSortKey> SortKeys(object?[]? keys) =>
        keys?.Cast<IDictionary>()
            .Select(key => new MdbSortKey(
                (string)key["field"]!,
                string.Equals(key["direction"] as string, "descending", StringComparison.OrdinalIgnoreCase)
                    ? MdbSortDirection.Descending
                    : MdbSortDirection.Ascending))
            .ToArray()
        ?? Array.Empty<MdbSortKey>();

    private static void AssertTypes(MdbCollection collection, ConformanceCase testCase)
    {
        var path = (string)testCase.Input["path"]!;
        var expectedTypes = ((object?[])testCase.Expect["types"]!).Cast<string>();

        Assert.True((bool)testCase.Expect["valid"]!);
        Assert.Equal(expectedTypes, collection.Records[path].MatchedTypes.Select(type => type.Name));
    }
}
