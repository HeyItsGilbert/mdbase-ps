using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class WherePredicateOperatorTests
{
    [Theory]
    [InlineData("priority:\n  gt: 2", "priority: 3", true)]
    [InlineData("priority:\n  gt: 2", "priority: 2", false)]
    [InlineData("tags:\n  contains: urgent", "tags: [urgent, later]", true)]
    [InlineData("tags:\n  contains: urgent", "tags: [later]", false)]
    [InlineData("title:\n  startsWith: Fix", "title: Fix login", true)]
    [InlineData("title:\n  matches: \"^[A-Z]\"", "title: lowercase", false)]
    [InlineData("status:\n  exists: true", "status: null", true)]
    [InlineData("status:\n  eq: open", "other: 1", false)]
    public void Where_operators_evaluate_per_spec(string whereClause, string frontmatterLine, bool expectedMatch)
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/probe.md", $"""
            ---
            kind: mdbase.type
            name: probe

            match:
              path_glob: "*.md"
              where:
                {whereClause.Replace("\n", "\n    ")}

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                additionalProperties: true
            ---
            """);
        fixture.WriteFile("record.md", $"---\n{frontmatterLine}\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var matched = collection.Records["record.md"].MatchedTypes.Any(t => t.Name == "probe");
        Assert.Equal(expectedMatch, matched);
    }

    [Fact]
    public void Fields_present_treats_empty_string_false_zero_and_empty_list_as_present()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/probe.md", """
            ---
            kind: mdbase.type
            name: probe

            match:
              path_glob: "*.md"
              fields_present: [a, b, c, d]

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                additionalProperties: true
            ---
            """);
        fixture.WriteFile("record.md", "---\na: \"\"\nb: false\nc: 0\nd: []\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "probe" }, collection.Records["record.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Fields_present_treats_missing_and_explicit_null_as_absent()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/probe.md", """
            ---
            kind: mdbase.type
            name: probe

            match:
              path_glob: "*.md"
              fields_present: [a]

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                additionalProperties: true
            ---
            """);
        fixture.WriteFile("missing.md", "---\nother: 1\n---\n");
        fixture.WriteFile("nulled.md", "---\na: null\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Records["missing.md"].MatchedTypes);
        Assert.Empty(collection.Records["nulled.md"].MatchedTypes);
    }

    [Fact]
    public void Json_pointer_field_references_resolve_nested_and_at_prefixed_keys()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/probe.md", """
            ---
            kind: mdbase.type
            name: probe

            match:
              path_glob: "*.md"
              fields_present: ["/@type", "/metadata/owner"]

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                additionalProperties: true
            ---
            """);
        fixture.WriteFile("record.md", "---\n\"@type\": note\nmetadata:\n  owner: me\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "probe" }, collection.Records["record.md"].MatchedTypes.Select(t => t.Name));
    }
}
