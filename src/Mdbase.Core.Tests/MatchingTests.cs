using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class MatchingTests
{
    [Fact]
    public void Inferred_match_combines_path_glob_fields_present_and_where_with_and()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/open-task.md", """
            ---
            kind: mdbase.type
            name: open-task

            match:
              path_glob: "tasks/**/*.md"
              fields_present: [title]
              where:
                status:
                  neq: done

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                additionalProperties: true
            ---
            """);

        // Matches: right path, title present, status != done.
        fixture.WriteFile("tasks/a.md", "---\ntitle: A\nstatus: open\n---\n");
        // Wrong path.
        fixture.WriteFile("notes/b.md", "---\ntitle: B\nstatus: open\n---\n");
        // Missing title.
        fixture.WriteFile("tasks/c.md", "---\nstatus: open\n---\n");
        // status == done fails the where clause.
        fixture.WriteFile("tasks/d.md", "---\ntitle: D\nstatus: done\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "open-task" }, collection.Records["tasks/a.md"].MatchedTypes.Select(t => t.Name));
        Assert.Empty(collection.Records["notes/b.md"].MatchedTypes);
        Assert.Empty(collection.Records["tasks/c.md"].MatchedTypes);
        Assert.Empty(collection.Records["tasks/d.md"].MatchedTypes);
    }

    [Fact]
    public void Record_matching_several_types_is_validated_independently_against_each()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", TypeWithSchema("a", "\"tasks/**/*.md\"", requireField: "title"));
        fixture.WriteFile("_types/b.md", TypeWithSchema("b", "\"tasks/**/*.md\"", requireField: "owner"));
        fixture.WriteFile("tasks/both-pass.md", "---\ntitle: T\nowner: me\n---\n");
        fixture.WriteFile("tasks/one-fails.md", "---\ntitle: T\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var bothPass = collection.Records["tasks/both-pass.md"];
        Assert.True(bothPass.IsValid);
        Assert.Equal(2, bothPass.MatchedTypes.Count);

        var oneFails = collection.Records["tasks/one-fails.md"];
        Assert.False(oneFails.IsValid);
        Assert.Contains(oneFails.ValidationDiagnostics, d => d.Code == "schema_required" && d.Type == "b");
    }

    [Fact]
    public void Explicit_type_declaration_takes_precedence_over_inferred_matching()
    {
        using var fixture = new TempCollection();
        // Would inferrably match "note" via path_glob, but the record explicitly declares "task".
        fixture.WriteFile("_types/task.md", ConnectTests.TaskType());
        fixture.WriteFile("_types/note.md", TypeWithSchema("note", "\"tasks/**/*.md\"", requireField: null));
        fixture.WriteFile("tasks/explicit.md", "---\ntype: task\ntitle: Explicit\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "task" }, collection.Records["tasks/explicit.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Explicit_declarations_concatenate_configured_keys_in_order_and_dedup_case_insensitively()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/alpha.md", TypeWithSchema("alpha", null, requireField: null));
        fixture.WriteFile("_types/beta.md", TypeWithSchema("beta", null, requireField: null));
        fixture.WriteFile("record.md", "---\ntype: Beta\ntypes: [beta, alpha]\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "beta", "alpha" }, collection.Records["record.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Inferred_matches_are_ordered_by_canonical_lower_case_type_name()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/zebra.md", TypeWithSchema("Zebra", "\"*.md\"", requireField: null));
        fixture.WriteFile("_types/apple.md", TypeWithSchema("Apple", "\"*.md\"", requireField: null));
        fixture.WriteFile("record.md", "---\ntitle: Untyped candidate\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "Apple", "Zebra" }, collection.Records["record.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Match_expr_ands_with_structured_match_members()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/urgent-task.md", """
            ---
            kind: mdbase.type
            name: urgent-task

            match:
              path_glob: "tasks/**/*.md"
              expr:
                $expr: "priority == 'high'"

            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("tasks/a.md", "---\npriority: high\n---\n");
        fixture.WriteFile("tasks/b.md", "---\npriority: low\n---\n");
        fixture.WriteFile("notes/c.md", "---\npriority: high\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "urgent-task" }, collection.Records["tasks/a.md"].MatchedTypes.Select(t => t.Name));
        Assert.Empty(collection.Records["tasks/b.md"].MatchedTypes);
        Assert.Empty(collection.Records["notes/c.md"].MatchedTypes);
    }

    [Fact]
    public void Match_expr_only_type_matches_on_the_expression_alone()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/tagged.md", """
            ---
            kind: mdbase.type
            name: tagged
            match:
              expr:
                $expr: "'urgent' in tags"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("a.md", "---\ntags: [urgent, work]\n---\n");
        fixture.WriteFile("b.md", "---\ntags: [work]\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "tagged" }, collection.Records["a.md"].MatchedTypes.Select(t => t.Name));
        Assert.Empty(collection.Records["b.md"].MatchedTypes);
    }

    [Fact]
    public void Malformed_match_expr_rejects_the_type_file_with_a_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/broken.md", """
            ---
            kind: mdbase.type
            name: broken
            match:
              expr:
                $expr: "status =="
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_invalid" && d.Path == "_types/broken.md");
    }

    [Fact]
    public void Match_expr_evaluation_error_excludes_the_candidate_without_aborting_others()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/numeric.md", """
            ---
            kind: mdbase.type
            name: numeric
            match:
              expr:
                $expr: "priority > 1"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        // priority is a string here, `> 1` throws a runtime type error against an int literal.
        fixture.WriteFile("bad.md", "---\npriority: high\n---\n");
        fixture.WriteFile("good.md", "---\npriority: 5\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Records["bad.md"].MatchedTypes);
        Assert.Contains(collection.Records["bad.md"].CompositionDiagnostics, d => d.Code == "match_expr_error" && d.Type == "numeric");
        Assert.Equal(new[] { "numeric" }, collection.Records["good.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Match_expr_evaluating_to_false_or_null_is_a_non_match()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/never.md", """
            ---
            kind: mdbase.type
            name: never
            match:
              expr:
                $expr: "false"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("_types/maybe.md", """
            ---
            kind: mdbase.type
            name: maybe
            match:
              expr:
                $expr: "does_not_exist_field"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("a.md", "---\ntitle: A\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Records["a.md"].MatchedTypes);
    }

    [Fact]
    public void Match_expr_sees_the_same_raw_frontmatter_context_as_structured_match()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/consistent.md", """
            ---
            kind: mdbase.type
            name: consistent
            match:
              expr:
                $expr: "present.raw.due == true && due == null && record.status == raw.status && note.status == 'open'"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);
        fixture.WriteFile("a.md", "---\nstatus: open\ndue: null\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "consistent" }, collection.Records["a.md"].MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Malformed_type_file_is_rejected_with_a_diagnostic_and_collection_still_loads()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/broken.md", """
            ---
            kind: mdbase.type
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """); // missing required 'name'
        fixture.WriteFile("record.md", "---\ntitle: Fine\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_invalid" && d.Path == "_types/broken.md");
        Assert.True(collection.Records["record.md"].IsValid);
    }

    [Fact]
    public void Duplicate_case_insensitive_type_names_produce_a_type_conflict_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task-1.md", TypeWithSchema("task", null, requireField: null));
        fixture.WriteFile("_types/task-2.md", TypeWithSchema("Task", null, requireField: null));

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_conflict");
    }

    internal static string TypeWithSchema(string name, string? pathGlob, string? requireField)
    {
        var matchSection = pathGlob is null ? string.Empty : $"""

            match:
              path_glob: {pathGlob}
            """;
        var requiredSection = requireField is null ? "[]" : $"[{requireField}]";
        return $"""
            ---
            kind: mdbase.type
            name: {name}
            {matchSection}
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                required: {requiredSection}
                additionalProperties: true
            ---
            """;
    }
}
