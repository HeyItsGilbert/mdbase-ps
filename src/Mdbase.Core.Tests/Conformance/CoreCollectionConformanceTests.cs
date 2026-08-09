using System.Collections;
using System.Collections.Specialized;
using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests.Conformance;

/// <summary>
/// Data-driven runner for the vendored `core_collection` v0.3 conformance fixture group
/// (#14/#30's Testing Decisions), driving `Mdbase.Core` directly through `MdbCollection`.
/// See `Fixtures/vendored/v0.3/VENDORED.md` for provenance and the excluded-case list — this
/// spec's engine doesn't implement Core Write or cross-file Uniqueness yet.
/// </summary>
public class CoreCollectionConformanceTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "vendored", "v0.3", "core", "core-collection.yaml");

    /// <summary>Test names/ids this spec's engine does not yet cover — see VENDORED.md for why.</summary>
    private static readonly HashSet<string> ExcludedByOperation = new() { "create" };

    private static readonly HashSet<string> ExcludedByName = new()
    {
        "collection unique detects duplicate ids",
    };

    public static IEnumerable<object[]> Cases()
    {
        var text = File.ReadAllText(FixturePath);
        foreach (var testCase in ConformanceCase.LoadAll(text))
        {
            if (ExcludedByOperation.Contains(testCase.Operation) || ExcludedByName.Contains(testCase.TestName))
            {
                continue;
            }

            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fixture_case_matches_expectation(ConformanceCase testCase)
    {
        using var fixture = new TempCollection(testCase.ConfigYaml);
        foreach (var (name, content) in testCase.Types)
        {
            fixture.WriteFile($"_types/{name}", content);
        }

        foreach (var (path, content) in testCase.Files)
        {
            fixture.WriteFile(path, content);
        }

        var collection = MdbCollection.Connect(fixture.RootPath);

        switch (testCase.Operation)
        {
            case "validate":
                AssertValidate(collection, testCase);
                break;
            case "read":
                AssertRead(collection, testCase);
                break;
            case "get_types":
                AssertGetTypes(collection, testCase);
                break;
            case "get_type":
                AssertGetType(collection, testCase);
                break;
            default:
                Assert.Fail($"Conformance case '{testCase}' uses unsupported operation '{testCase.Operation}'.");
                break;
        }
    }

    private static void AssertValidate(MdbCollection collection, ConformanceCase testCase)
    {
        var path = (string)testCase.Input["path"]!;
        var record = collection.Records[path];
        var expectedValid = (bool)testCase.Expect["valid"]!;

        // The vendored operation's `valid` combines every diagnostic source (schema, plus #9's
        // link-target constraints); `MdbRecord.IsValid` stays schema-only by design (#38 keeps
        // `LinkDiagnostics` distinct from `ValidationDiagnostics`), so the adapter folds
        // error-severity link diagnostics in here rather than on the Core type itself.
        var hasErrorLinkDiagnostic = record.LinkDiagnostics.Any(d => d.Severity == MdbSeverity.Error);
        Assert.Equal(expectedValid, record.IsValid && !hasErrorLinkDiagnostic);

        var allDiagnostics = record.ValidationDiagnostics.Concat(record.LinkDiagnostics).ToArray();
        if (testCase.Expect["issues"] is object?[] { Length: 0 })
        {
            Assert.Empty(allDiagnostics);
        }
        else if (testCase.Expect["issues"] is object?[] issues)
        {
            foreach (var issueNode in issues)
            {
                var issue = (OrderedDictionary)issueNode!;
                var code = (string)issue["code"]!;
                var field = issue["field"] as string;
                Assert.Contains(
                    allDiagnostics,
                    d => d.Code == code && (field is null || d.Field == field));
            }
        }

        if (testCase.Expect["types"] is object?[] expectedTypes)
        {
            Assert.Equal(expectedTypes.Cast<string>(), record.MatchedTypes.Select(t => t.Name));
        }

        if (testCase.Expect["resolved_links"] is OrderedDictionary resolvedLinks)
        {
            foreach (DictionaryEntry entry in resolvedLinks)
            {
                var fieldName = (string)entry.Key;
                var expectedTarget = (string)entry.Value!;
                Assert.Contains(
                    collection.GetBacklinks(expectedTarget),
                    backlink => backlink.SourcePath == path && backlink.FieldPath == fieldName);
            }
        }
    }

    private static void AssertRead(MdbCollection collection, ConformanceCase testCase)
    {
        // Per spec Ch.12, plain `read` succeeds independently of JSON Schema validity (that's
        // the separate `validate` operation) — `expect.valid` here means "the read succeeded",
        // which a resolved record lookup below already establishes.
        var path = (string)testCase.Input["path"]!;
        var record = collection.Records[path];
        if (testCase.Expect["effective_frontmatter"] is OrderedDictionary expectedEffective)
        {
            foreach (DictionaryEntry entry in expectedEffective)
            {
                Assert.True(record.EffectiveFrontmatter.Contains(entry.Key), $"Expected effective field '{entry.Key}'.");
                Assert.Equal(entry.Value, record.EffectiveFrontmatter[entry.Key]);
            }
        }

        if (testCase.Expect["frontmatter_not_contains"] is object?[] absentFields)
        {
            foreach (var field in absentFields.Cast<string>())
            {
                Assert.False(record.Frontmatter.Contains(field), $"Persisted frontmatter should not contain '{field}'.");
            }
        }
    }

    private static void AssertGetTypes(MdbCollection collection, ConformanceCase testCase)
    {
        var path = (string)testCase.Input["path"]!;
        var record = collection.Records[path];

        if (testCase.Expect["types"] is object?[] expectedTypes)
        {
            Assert.Equal(expectedTypes.Cast<string>(), record.MatchedTypes.Select(t => t.Name));
        }
    }

    private static void AssertGetType(MdbCollection collection, ConformanceCase testCase)
    {
        var name = (string)testCase.Input["name"]!;
        var type = collection.Types[name.ToLowerInvariant()];

        if (testCase.Expect["type"] is OrderedDictionary expectedType)
        {
            if (expectedType["name"] is string expectedName)
            {
                Assert.Equal(expectedName, type.Name);
            }

            if (expectedType["collection"] is OrderedDictionary expectedCollection
                && expectedCollection["display"] is OrderedDictionary expectedDisplay
                && expectedDisplay["name_field"] is string expectedNameField)
            {
                var display = (OrderedDictionary?)type.CollectionSection?["display"];
                Assert.Equal(expectedNameField, display?["name_field"]);
            }
        }
    }
}
