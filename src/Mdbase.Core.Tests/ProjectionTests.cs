using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class ProjectionTests
{
    [Fact]
    public void Projection_populates_a_genuinely_missing_field()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            match:
              path_glob: "*.md"
            collection:
              projections:
                slug:
                  expr: "title.lowerAscii()"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("a.md", "---\ntitle: Hello World\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.Equal("hello world", record.EffectiveFrontmatter["slug"]);
        Assert.Equal(MdbPresentState.Effective, record.Present["slug"]);
    }

    [Fact]
    public void Projection_referencing_an_earlier_projection_sees_its_resolved_value()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            match:
              path_glob: "*.md"
            collection:
              projections:
                slug:
                  expr: "title.lowerAscii()"
                permalink:
                  expr: "'/tasks/' + slug"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("a.md", "---\ntitle: Hello World\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.Equal("hello world", record.EffectiveFrontmatter["slug"]);
        Assert.Equal("/tasks/hello world", record.EffectiveFrontmatter["permalink"]);
    }

    [Fact]
    public void Projection_dependency_cycle_rejects_the_type_file()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            collection:
              projections:
                a:
                  expr: "b + 1"
                b:
                  expr: "a + 1"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_invalid" && d.Path == "_types/task.md");
    }

    [Fact]
    public void Identical_projection_source_across_types_coalesces_without_a_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", TaskWithProjection("a", "slug", "title.lowerAscii()"));
        fixture.WriteFile("_types/b.md", TaskWithProjection("b", "slug", "title.lowerAscii()"));
        fixture.WriteFile("x.md", "---\ntype: [a, b]\ntitle: Hi There\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["x.md"];

        Assert.Equal("hi there", record.EffectiveFrontmatter["slug"]);
        Assert.DoesNotContain(record.CompositionDiagnostics, d => d.Code == "type_conflict");
    }

    [Fact]
    public void Differing_projection_source_across_types_conflicts_and_leaves_the_field_unavailable()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", TaskWithProjection("a", "slug", "title.lowerAscii()"));
        fixture.WriteFile("_types/b.md", TaskWithProjection("b", "slug", "title.upperAscii()"));
        fixture.WriteFile("x.md", "---\ntype: [a, b]\ntitle: Hi There\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["x.md"];

        Assert.False(record.EffectiveFrontmatter.Contains("slug"));
        Assert.Equal(MdbPresentState.Missing, record.Present["slug"]);
        Assert.Contains(record.CompositionDiagnostics, d => d.Code == "type_conflict" && d.Field == "slug");
    }

    [Fact]
    public void Projection_targeting_an_already_present_raw_field_is_not_evaluated()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskWithProjection("task", "slug", "title.lowerAscii()"));
        fixture.WriteFile("a.md", "---\ntitle: Hello\nslug: author-chosen\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.Equal("author-chosen", record.EffectiveFrontmatter["slug"]);
        Assert.Equal(MdbPresentState.Raw, record.Present["slug"]);
    }

    [Fact]
    public void Projection_evaluation_error_is_a_per_record_diagnostic_leaving_the_field_missing()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskWithProjection("task", "shout", "title.upperAscii()"));
        // title is missing entirely on this record, so `title.upperAscii()` errors at runtime
        // (null has no upperAscii()).
        fixture.WriteFile("a.md", "---\nother: value\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.False(record.EffectiveFrontmatter.Contains("shout"));
        Assert.Equal(MdbPresentState.Missing, record.Present["shout"]);
        Assert.Contains(record.CompositionDiagnostics, d => d.Code == "projection_error" && d.Field == "shout");
    }

    private static string TaskWithProjection(string typeName, string field, string expr) => $$"""
        ---
        kind: mdbase.type
        name: {{typeName}}
        match:
          path_glob: "*.md"
        collection:
          projections:
            {{field}}:
              expr: "{{expr}}"
        schema:
          dialect: json-schema-2020-12
          value:
            type: object
        ---
        """;
}
