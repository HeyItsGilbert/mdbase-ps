using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class EffectiveFrontmatterTests
{
    [Fact]
    public void Read_default_applies_only_to_a_missing_field_never_an_explicit_null()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", DefaultingType("task", "status", "open"));
        fixture.WriteFile("missing.md", "---\ntype: task\ntitle: A\n---\n");
        fixture.WriteFile("explicit-null.md", "---\ntype: task\ntitle: B\nstatus: null\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var missing = collection.Records["missing.md"];
        Assert.Equal("open", missing.EffectiveFrontmatter["status"]);
        Assert.False(missing.Frontmatter.Contains("status"));
        Assert.Equal(MdbPresentState.Effective, missing.Present["status"]);

        var explicitNull = collection.Records["explicit-null.md"];
        Assert.Null(explicitNull.EffectiveFrontmatter["status"]);
        Assert.True(explicitNull.Frontmatter.Contains("status"));
        Assert.Equal(MdbPresentState.Null, explicitNull.Present["status"]);
    }

    [Fact]
    public void Identical_read_defaults_across_matched_types_coalesce_without_diagnostics()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", DefaultingType("a", "status", "open"));
        fixture.WriteFile("_types/b.md", DefaultingType("b", "status", "open"));
        fixture.WriteFile("record.md", "---\ntypes: [a, b]\ntitle: X\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var record = collection.Records["record.md"];
        Assert.Equal("open", record.EffectiveFrontmatter["status"]);
        Assert.Empty(record.CompositionDiagnostics);
    }

    [Fact]
    public void Differing_read_defaults_across_matched_types_report_type_conflict_and_leave_field_unavailable()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", DefaultingType("a", "status", "open"));
        fixture.WriteFile("_types/b.md", DefaultingType("b", "status", "draft"));
        fixture.WriteFile("record.md", "---\ntypes: [a, b]\ntitle: X\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var record = collection.Records["record.md"];
        Assert.False(record.EffectiveFrontmatter.Contains("status"));
        Assert.Equal(MdbPresentState.Missing, record.Present["status"]);
        var conflict = Assert.Single(record.CompositionDiagnostics);
        Assert.Equal("type_conflict", conflict.Code);
        Assert.Equal("status", conflict.Field);
    }

    [Fact]
    public void Present_distinguishes_raw_from_effective_and_missing_fields()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", DefaultingType("task", "status", "open"));
        fixture.WriteFile("record.md", "---\ntype: task\ntitle: Explicit raw value\nstatus: closed\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["record.md"];

        Assert.Equal(MdbPresentState.Raw, record.Present["title"]);
        Assert.Equal(MdbPresentState.Raw, record.Present["status"]);
        Assert.Equal(MdbPresentState.Missing, record.Present["nonexistent-field"]);
    }

    private static string DefaultingType(string name, string defaultField, string defaultValue) => $"""
        ---
        kind: mdbase.type
        name: {name}

        schema:
          dialect: json-schema-2020-12
          value:
            type: object
            additionalProperties: true

        collection:
          read_defaults:
            {defaultField}: {defaultValue}
        ---
        """;
}
