using Mdbase.Core.Query;
using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class QueryEngineTests
{
    private static MdbCollection ConnectTaskCollection(TempCollection fixture)
    {
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            match:
              path_glob: "tasks/**/*.md"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        return MdbCollection.Connect(fixture.RootPath);
    }

    [Fact]
    public void Types_filter_ors_across_matched_type_membership()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            match:
              path_glob: "tasks/*.md"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("_types/note.md", """
            ---
            kind: mdbase.type
            name: note
            match:
              path_glob: "notes/*.md"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        fixture.WriteFile("notes/b.md", "---\ntitle: B\n---\n");
        fixture.WriteFile("other.md", "---\ntitle: Untyped\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var typed = MdbCompiledQuery.Compile(new MdbQuery { Types = new[] { "task" } }).Execute(collection);
        Assert.Equal(new[] { "tasks/a.md" }, typed.Results.Select(r => r.FileInfo.Path));

        var all = MdbCompiledQuery.Compile(new MdbQuery()).Execute(collection);
        Assert.Equal(3, all.Results.Count);
    }

    [Fact]
    public void Context_binds_this_and_missing_context_throws()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var result = MdbCompiledQuery.Compile(new MdbQuery { ContextPath = "tasks/a.md" }).Execute(collection);
        Assert.Equal("tasks/a.md", result.Meta.Context);

        var compiled = MdbCompiledQuery.Compile(new MdbQuery { ContextPath = "tasks/missing.md" });
        Assert.Throws<MdbQueryContextNotFoundException>(() => compiled.Execute(collection));
    }

    [Fact]
    public void Note_aliases_effective_values_in_query_context_read_defaults_included()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            match:
              path_glob: "tasks/**/*.md"
            collection:
              read_defaults:
                status: open
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        // status is genuinely missing from raw, so raw.status is null but note.status (an
        // alias for effective `record`) sees the read default.
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var result = MdbCompiledQuery.Compile(new MdbQuery { Where = "note.status == 'open' && raw.status == null" }).Execute(collection);

        Assert.Single(result.Results);
    }

    [Fact]
    public void This_field_mirrors_this_record_field_for_effective_context_values()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/ctx.md", "---\ntitle: Context\n---\n");
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            ContextPath = "tasks/ctx.md",
            Where = "this.title == 'Context' && this.record.title == 'Context' && this.note.title == 'Context'",
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public void Named_projections_evaluate_in_dependency_order_and_are_visible_to_where()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: Hello\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            Projections = new Dictionary<string, string>
            {
                ["upper"] = "title.upperAscii()",
                ["shout"] = "projection.upper + '!'",
            },
            Where = "projection.shout == 'HELLO!'",
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Single(result.Results);
    }

    [Fact]
    public void Projection_dependency_cycle_is_rejected_at_compile_time()
    {
        var query = new MdbQuery
        {
            Projections = new Dictionary<string, string>
            {
                ["a"] = "projection.b + 1",
                ["b"] = "projection.a + 1",
            },
        };

        Assert.Throws<MdbInvalidQueryException>(() => MdbCompiledQuery.Compile(query));
    }

    [Fact]
    public void Where_true_false_null_and_error_filter_correctly()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/pass.md", "---\nstatus: open\n---\n");
        fixture.WriteFile("tasks/fail.md", "---\nstatus: done\n---\n");
        fixture.WriteFile("tasks/null.md", "---\nother: value\n---\n"); // status missing -> null == 'open' is false
        fixture.WriteFile("tasks/error.md", "---\nstatus: 5\n---\n"); // status is int, .startsWith errors
        var collection = MdbCollection.Connect(fixture.RootPath);

        var result = MdbCompiledQuery.Compile(new MdbQuery { Where = "status == 'open'" }).Execute(collection);
        Assert.Equal(new[] { "tasks/pass.md" }, result.Results.Select(r => r.FileInfo.Path));

        var errored = MdbCompiledQuery.Compile(new MdbQuery { Where = "status.startsWith('o')" }).Execute(collection);
        Assert.Contains(errored.Diagnostics, d => d.Code == "where_error" && d.Path == "tasks/error.md");
        Assert.DoesNotContain(errored.Results, r => r.FileInfo.Path == "tasks/error.md");
    }

    [Fact]
    public void Select_supports_bare_field_and_named_expression_forms_and_rejects_duplicates()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: Hello\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            Select = new[]
            {
                new MdbSelectItem("title", "title"),
                new MdbSelectItem("shout", "title.upperAscii()"),
            },
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);
        var row = Assert.Single(result.Results);
        Assert.Equal("Hello", row.Values["title"]);
        Assert.Equal("HELLO", row.Values["shout"]);

        var dup = new MdbQuery { Select = new[] { new MdbSelectItem("x", "1"), new MdbSelectItem("x", "2") } };
        Assert.Throws<MdbInvalidQueryException>(() => MdbCompiledQuery.Compile(dup));
    }

    [Fact]
    public void Order_by_places_nulls_last_ascending_first_descending_with_file_path_tiebreak()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/b.md", "---\npriority: 2\n---\n");
        fixture.WriteFile("tasks/a.md", "---\npriority: 1\n---\n");
        fixture.WriteFile("tasks/c.md", "---\nother: value\n---\n"); // priority missing -> null
        fixture.WriteFile("tasks/d.md", "---\npriority: 1\n---\n"); // ties with a.md, breaks by path
        var collection = MdbCollection.Connect(fixture.RootPath);

        var ascending = MdbCompiledQuery.Compile(new MdbQuery { OrderBy = new[] { new MdbSortKey("priority") } }).Execute(collection);
        Assert.Equal(new[] { "tasks/a.md", "tasks/d.md", "tasks/b.md", "tasks/c.md" }, ascending.Results.Select(r => r.FileInfo.Path));

        var descending = MdbCompiledQuery.Compile(new MdbQuery { OrderBy = new[] { new MdbSortKey("priority", MdbSortDirection.Descending) } }).Execute(collection);
        Assert.Equal(new[] { "tasks/c.md", "tasks/b.md", "tasks/a.md", "tasks/d.md" }, descending.Results.Select(r => r.FileInfo.Path));
    }

    [Fact]
    public void Group_by_forms_a_null_group_and_computes_per_group_summaries()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\nstatus: open\nhours: 2\n---\n");
        fixture.WriteFile("tasks/b.md", "---\nstatus: open\nhours: 3\n---\n");
        fixture.WriteFile("tasks/c.md", "---\nhours: 5\n---\n"); // status missing -> null group
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            GroupBy = new[] { new MdbSortKey("status") },
            Summaries = new[] { new MdbSummaryRequest("hours", "sum") },
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.NotNull(result.Meta.Groups);
        var openGroup = Assert.Single(result.Meta.Groups!, g => Equals(g.Values["status"], "open"));
        Assert.Equal(2, openGroup.Count);
        Assert.Equal(5.0, openGroup.Summaries["sum_hours"]);

        var nullGroup = Assert.Single(result.Meta.Groups!, g => g.Values["status"] is null);
        Assert.Equal(1, nullGroup.Count);
    }

    [Theory]
    [InlineData("count", 3.0)]
    [InlineData("sum", 6.0)]
    [InlineData("average", 2.0)]
    [InlineData("minimum", 1.0)]
    [InlineData("maximum", 3.0)]
    [InlineData("empty", 0.0)]
    [InlineData("filled", 3.0)]
    public void Built_in_summary_functions_compute_expected_values(string function, double expected)
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\nhours: 1\n---\n");
        fixture.WriteFile("tasks/b.md", "---\nhours: 2\n---\n");
        fixture.WriteFile("tasks/c.md", "---\nhours: 3\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery { Summaries = new[] { new MdbSummaryRequest("hours", function) } };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Equal(expected, Convert.ToDouble(result.Meta.Summaries![$"{function}_hours"]));
    }

    [Fact]
    public void Sum_on_a_non_numeric_column_reports_a_summary_incompatible_value_diagnostic()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\nhours: not-a-number\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery { Summaries = new[] { new MdbSummaryRequest("hours", "sum") } };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Contains(result.Diagnostics, d => d.Code == "summary_incompatible_value");
    }

    [Fact]
    public void Custom_summary_function_receives_the_ordered_values_list()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\nhours: 1\n---\n");
        fixture.WriteFile("tasks/b.md", "---\nhours: 2\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            SummaryFunctions = new Dictionary<string, string> { ["double_sum"] = "values.map(v, v * 2).exists(v, true) ? values[0] * 2 + values[1] * 2 : 0" },
            Summaries = new[] { new MdbSummaryRequest("hours", "double_sum") },
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Equal(6L, result.Meta.Summaries!["double_sum_hours"]);
    }

    [Fact]
    public void Limit_zero_returns_no_rows_but_full_metadata()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        fixture.WriteFile("tasks/b.md", "---\ntitle: B\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var result = MdbCompiledQuery.Compile(new MdbQuery { Limit = 0 }).Execute(collection);

        Assert.Empty(result.Results);
        Assert.Equal(2, result.Meta.TotalCount);
        Assert.True(result.Meta.HasMore);
    }

    [Theory]
    [InlineData(MdbFrontmatterMode.Effective, true, false)]
    [InlineData(MdbFrontmatterMode.Persisted, false, true)]
    [InlineData(MdbFrontmatterMode.Both, true, true)]
    public void Frontmatter_mode_controls_only_result_serialization(MdbFrontmatterMode mode, bool expectEffective, bool expectPersisted)
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var result = MdbCompiledQuery.Compile(new MdbQuery { FrontmatterMode = mode }).Execute(collection);
        var row = Assert.Single(result.Results);

        Assert.Equal(expectEffective, row.EffectiveFrontmatter is not null);
        Assert.Equal(expectPersisted, row.Frontmatter is not null);
    }

    [Fact]
    public void Full_result_envelope_matches_the_canonical_shape_end_to_end()
    {
        using var fixture = new TempCollection();
        ConnectTaskCollection(fixture);
        fixture.WriteFile("tasks/a.md", "---\ntitle: Alpha\npriority: 1\n---\n");
        fixture.WriteFile("tasks/b.md", "---\ntitle: Beta\npriority: 2\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);

        var query = new MdbQuery
        {
            Select = new[] { new MdbSelectItem("title", "title") },
            OrderBy = new[] { new MdbSortKey("priority") },
            Limit = 1,
        };
        var result = MdbCompiledQuery.Compile(query).Execute(collection);

        Assert.Single(result.Results);
        Assert.Equal("tasks/a.md", result.Results[0].FileInfo.Path);
        Assert.Equal("Alpha", result.Results[0].Values["title"]);
        Assert.Equal(2, result.Meta.TotalCount);
        Assert.True(result.Meta.HasMore);
        Assert.Empty(result.Diagnostics);
    }
}
