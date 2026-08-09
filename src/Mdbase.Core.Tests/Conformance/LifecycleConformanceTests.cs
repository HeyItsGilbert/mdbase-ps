using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests.Conformance;

/// <summary>
/// Data-driven runner for the vendored `lifecycle` v0.3 conformance fixture group (#41's
/// Testing Decisions), driving `Mdbase.Core` directly through <see cref="MdbCollection"/>'s
/// `Create`/`Update`/`Records` seams. See `Fixtures/vendored/v0.3/VENDORED.md` for provenance.
/// </summary>
public class LifecycleConformanceTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "vendored", "v0.3", "lifecycle", "lifecycle.yaml");

    public static IEnumerable<object[]> Cases() =>
        ConformanceCase.LoadAll(File.ReadAllText(FixturePath)).Select(testCase => new object[] { testCase });

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
        var expectedValid = (bool)testCase.Expect["valid"]!;

        switch (testCase.Operation)
        {
            case "create":
                AssertCreate(collection, testCase, expectedValid);
                break;
            case "update":
                AssertUpdate(collection, testCase, expectedValid);
                break;
            case "read":
                AssertRead(collection, testCase);
                break;
            default:
                Assert.Fail($"Conformance case '{testCase}' uses unsupported operation '{testCase.Operation}'.");
                break;
        }
    }

    private static void AssertCreate(MdbCollection collection, ConformanceCase testCase, bool expectedValid)
    {
        var path = (string)testCase.Input["path"]!;
        var frontmatter = (OrderedDictionary)testCase.Input["frontmatter"]!;
        var type = testCase.Input["type"] as string;

        try
        {
            var record = collection.Create(frontmatter, types: type is null ? null : new[] { type }, path: path);
            Assert.True(expectedValid, $"Expected create('{path}') to fail but it succeeded.");
            AssertFrontmatterExpectations(record.Frontmatter, testCase.Expect);
        }
        catch (MdbWriteException) when (!expectedValid)
        {
            // expected failure
        }
    }

    private static void AssertUpdate(MdbCollection collection, ConformanceCase testCase, bool expectedValid)
    {
        var path = (string)testCase.Input["path"]!;
        var before = collection.Records.TryGetValue(path, out var existing) ? existing.Frontmatter : null;
        var patch = testCase.Input["patch"] as OrderedDictionary;

        try
        {
            var record = collection.Update(path, patch: patch);
            Assert.True(expectedValid, $"Expected update('{path}') to fail but it succeeded.");
            AssertFrontmatterExpectations(record.Frontmatter, testCase.Expect);

            if (testCase.Expect["frontmatter_changed"] is object?[] changedFields && before is not null)
            {
                foreach (var field in changedFields.Cast<string>())
                {
                    Assert.True(before.Contains(field), $"Baseline frontmatter should already contain '{field}'.");
                    Assert.NotEqual(before[field], record.Frontmatter[field]);
                }
            }
        }
        catch (MdbWriteException ex) when (!expectedValid)
        {
            if (testCase.Expect["issues"] is object?[] issues)
            {
                foreach (var issueNode in issues)
                {
                    var issue = (OrderedDictionary)issueNode!;
                    var code = (string)issue["code"]!;
                    var field = issue["field"] as string;
                    Assert.Equal(code, ex.Diagnostic.Code);
                    if (field is not null)
                    {
                        Assert.Equal(field, ex.Diagnostic.Field);
                    }
                }
            }
        }
    }

    private static void AssertRead(MdbCollection collection, ConformanceCase testCase)
    {
        var path = (string)testCase.Input["path"]!;
        var record = collection.Records[path];
        if (testCase.Expect["effective_frontmatter"] is OrderedDictionary expectedEffective)
        {
            foreach (DictionaryEntry entry in expectedEffective)
            {
                Assert.True(record.EffectiveFrontmatter.Contains(entry.Key));
                Assert.Equal(entry.Value, record.EffectiveFrontmatter[entry.Key]);
            }
        }
    }

    private static void AssertFrontmatterExpectations(OrderedDictionary frontmatter, OrderedDictionary expect)
    {
        if (expect["frontmatter_contains"] is OrderedDictionary contains)
        {
            foreach (DictionaryEntry entry in contains)
            {
                var field = (string)entry.Key;
                Assert.True(frontmatter.Contains(field), $"Expected frontmatter field '{field}'.");
                var actual = frontmatter[field];
                switch (entry.Value)
                {
                    case OrderedDictionary spec when spec["matches"] is string pattern:
                        Assert.Matches(pattern, (string)actual!);
                        break;
                    case OrderedDictionary spec when spec["format"] is string format:
                        AssertFormat(format, (string)actual!);
                        break;
                    default:
                        Assert.Equal(entry.Value, actual);
                        break;
                }
            }
        }

        if (expect["frontmatter_not_contains"] is object?[] absentFields)
        {
            foreach (var field in absentFields.Cast<string>())
            {
                Assert.False(frontmatter.Contains(field), $"Frontmatter should not contain '{field}'.");
            }
        }

        if (expect["frontmatter"] is OrderedDictionary exact)
        {
            foreach (DictionaryEntry entry in exact)
            {
                Assert.Equal(entry.Value, frontmatter[(string)entry.Key]);
            }
        }
    }

    private static void AssertFormat(string format, string value)
    {
        switch (format)
        {
            case "date-time":
                Assert.True(DateTimeOffset.TryParse(value, out _), $"'{value}' is not a valid date-time.");
                break;
            case "date":
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", value);
                break;
            default:
                Assert.Fail($"Unsupported format assertion '{format}'.");
                break;
        }
    }
}
