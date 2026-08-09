using System.Collections.Specialized;
using Mdbase.Core.Tests.Fixtures;
using Mdbase.Core.Write;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Tests;

public class WriteTests
{
    private static string ItemType(string? pathPattern = "items/{id}.md", string? uniqueYaml = null) => $$"""
        ---
        kind: mdbase.type
        name: item
        version: 1
        match:
          path_glob: "items/**/*.md"
        schema:
          dialect: json-schema-2020-12
          value:
            type: object
            required: [id, title]
            additionalProperties: true
            properties:
              type: { const: item }
              id: { type: string }
              title: { type: string }
              slug: { type: string }
              category: { type: [string, "null"] }
        collection:
          {{(pathPattern is null ? "" : $"path:\n    pattern: \"{pathPattern}\"")}}
          {{uniqueYaml ?? ""}}
        ---
        """;

    private static MdbCollection Connect(TempCollection fixture) => MdbCollection.Connect(fixture.RootPath);

    private static OrderedDictionary Fm(params (string Key, object? Value)[] entries)
    {
        var map = new OrderedDictionary();
        foreach (var (key, value) in entries)
        {
            map[key] = value;
        }

        return map;
    }

    // --- Create: explicit vs inferred type membership --------------------------------------

    [Fact]
    public void Create_with_explicit_type_generates_path_from_pattern_and_persists()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var record = collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "First")), types: new[] { "item" });

        Assert.Equal("items/abc.md", record.FileInfo.Path);
        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.Same(record, collection.Records["items/abc.md"]);
    }

    [Fact]
    public void Create_with_inferred_match_resolves_type_from_path_glob()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var record = collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "First")), path: "items/abc.md");

        Assert.Equal(new[] { "item" }, record.MatchedTypes.Select(t => t.Name));
    }

    [Fact]
    public void Create_type_membership_changed_when_lifecycle_flips_membership_aborts()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/draft.md", """
            ---
            kind: mdbase.type
            name: draft
            version: 1
            match:
              where:
                published:
                  exists: false
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                properties:
                  title: { type: string }
            lifecycle:
              on_create:
                set:
                  published: { literal: true }
            ---
            """);
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("title", "X")), path: "notes/x.md"));
        Assert.Equal("type_membership_changed", ex.Diagnostic.Code);
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "notes/x.md")));
    }

    // --- Path policy -------------------------------------------------------------------------

    [Fact]
    public void Create_path_value_missing_when_placeholder_field_is_absent()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType(pathPattern: "items/{slug}.md"));
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "No slug")), types: new[] { "item" }));
        Assert.Equal("path_value_missing", ex.Diagnostic.Code);
        Assert.Equal("slug", ex.Diagnostic.Field);
    }

    [Fact]
    public void Create_invalid_path_component_when_placeholder_value_contains_a_slash()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "a/b"), ("title", "Bad id")), types: new[] { "item" }));
        Assert.Equal("invalid_path_component", ex.Diagnostic.Code);
    }

    [Fact]
    public void Create_no_policy_available_when_no_explicit_path_and_no_pattern_declared()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType(pathPattern: null));
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "X")), types: new[] { "item" }));
        Assert.Equal("no_policy_available", ex.Diagnostic.Code);
    }

    [Fact]
    public void Create_rejects_a_path_that_already_has_a_file()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Existing\n---\n");
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "New")), types: new[] { "item" }));
        Assert.Equal("path_conflict", ex.Diagnostic.Code);
    }

    [Fact]
    public void Create_cross_type_conflicting_path_patterns_abort_with_type_conflict()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", """
            ---
            kind: mdbase.type
            name: type_a
            version: 1
            match:
              fields_present: [title]
            schema:
              dialect: json-schema-2020-12
              value: { type: object, properties: { title: { type: string } } }
            collection:
              path:
                pattern: "a/{title}.md"
            ---
            """);
        fixture.WriteFile("_types/b.md", """
            ---
            kind: mdbase.type
            name: type_b
            version: 1
            match:
              fields_present: [title]
            schema:
              dialect: json-schema-2020-12
              value: { type: object, properties: { title: { type: string } } }
            collection:
              path:
                pattern: "b/{title}.md"
            ---
            """);
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("title", "conflict"))));
        Assert.Equal("type_conflict", ex.Diagnostic.Code);
    }

    [Fact]
    public void Create_boundary_check_rejects_an_escaping_explicit_path()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "x"), ("title", "X")), path: "../escape.md"));
        Assert.Equal("path_traversal", ex.Diagnostic.Code);
    }

    // --- Uniqueness --------------------------------------------------------------------------

    [Fact]
    public void Create_collection_scoped_uniqueness_rejects_a_duplicate_id()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType(uniqueYaml: "unique:\n    - field: id\n      scope: collection"));
        fixture.WriteFile("items/one.md", "---\ntype: item\nid: dup\ntitle: One\n---\n");
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "dup"), ("title", "Two")), path: "items/two.md"));
        Assert.Equal("unique_conflict", ex.Diagnostic.Code);
    }

    [Fact]
    public void Create_uniqueness_exempts_missing_or_null_values()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType(uniqueYaml: "unique:\n    - field: category\n      scope: collection"));
        fixture.WriteFile("items/one.md", "---\ntype: item\nid: one\ntitle: One\ncategory:\n---\n");
        var collection = Connect(fixture);

        var record = collection.Create(Fm(("type", "item"), ("id", "two"), ("title", "Two"), ("category", null)), path: "items/two.md");
        Assert.Equal("two", record.Frontmatter["id"]);
    }

    [Fact]
    public void Create_path_glob_scoped_uniqueness_only_compares_records_under_the_glob()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType(pathPattern: null, uniqueYaml: "unique:\n    - field: id\n      scope: path_glob\n      path_glob: \"items/a/**\""));
        fixture.WriteFile("items/a/one.md", "---\ntype: item\nid: taken\ntitle: One\n---\n");
        var collection = Connect(fixture);

        // A value that doesn't appear anywhere under items/a/** never collides, even for a
        // record whose own path lies outside the glob.
        var record = collection.Create(Fm(("type", "item"), ("id", "fresh"), ("title", "New")), path: "items/b/new.md");
        Assert.Equal("fresh", record.Frontmatter["id"]);

        // The comparison set is every record whose path matches the glob, regardless of the new
        // record's own path — a duplicate against items/a/one.md conflicts even from items/b/.
        var ex = Assert.Throws<MdbWriteException>(() => collection.Create(Fm(("type", "item"), ("id", "taken"), ("title", "Clash")), path: "items/b/clash.md"));
        Assert.Equal("unique_conflict", ex.Diagnostic.Code);
    }

    // --- Update: patch / document replacement / concurrency ---------------------------------

    [Fact]
    public void Update_patch_sets_nulls_and_removes_are_applied_and_other_keys_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Original\ncategory: books\nnote: keep\n---\nbody text\n");
        var collection = Connect(fixture);

        var record = collection.Update(
            "items/abc.md",
            patch: Fm(("title", "Updated"), ("category", null)),
            remove: new[] { "note" });

        Assert.Equal("Updated", record.Frontmatter["title"]);
        Assert.True(record.Frontmatter.Contains("category"));
        Assert.Null(record.Frontmatter["category"]);
        Assert.False(record.Frontmatter.Contains("note"));
        Assert.Equal("abc", record.Frontmatter["id"]);
        Assert.Equal("body text\n", record.Body);
    }

    [Fact]
    public void Update_document_replacement_preserves_exact_bytes_when_lifecycle_changes_nothing()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Original\n---\nold body\n");
        var collection = Connect(fixture);

        const string replacement = "---\ntype: item\nid: abc\ntitle: Replaced\n---\nnew body\n";
        collection.Update("items/abc.md", document: replacement);

        var persisted = File.ReadAllText(Path.Combine(fixture.RootPath, "items/abc.md"));
        Assert.Equal(replacement, persisted);
    }

    [Fact]
    public void Update_document_replacement_reserializes_when_lifecycle_mutates_a_value()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", """
            ---
            kind: mdbase.type
            name: item
            version: 1
            match:
              path_glob: "items/**/*.md"
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                properties:
                  id: { type: string }
                  title: { type: string }
                  touched: { type: string }
            lifecycle:
              on_update:
                set:
                  touched: { literal: "yes" }
            ---
            """);
        fixture.WriteFile("items/abc.md", "---\nid: abc\ntitle: Original\n---\nbody\n");
        var collection = Connect(fixture);

        const string replacement = "---\nid: abc\ntitle: Replaced\n---\nbody\n";
        var record = collection.Update("items/abc.md", document: replacement);

        Assert.Equal("yes", record.Frontmatter["touched"]);
        var persisted = File.ReadAllText(Path.Combine(fixture.RootPath, "items/abc.md"));
        Assert.NotEqual(replacement, persisted);
        Assert.Contains("touched: yes", persisted);
    }

    [Fact]
    public void Update_throws_when_document_combined_with_patch()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Original\n---\n");
        var collection = Connect(fixture);

        Assert.Throws<ArgumentException>(() => collection.Update("items/abc.md", document: "---\nid: abc\n---\n", patch: Fm(("title", "X"))));
    }

    [Fact]
    public void Update_matching_if_revision_succeeds_and_mismatched_if_revision_fails_and_leaves_file_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Original\n---\n");
        var collection = Connect(fixture);
        var revision = collection.Records["items/abc.md"].Revision;

        var ex = Assert.Throws<MdbWriteException>(() => collection.Update("items/abc.md", patch: Fm(("title", "Nope")), ifRevision: "sha256:0000"));
        Assert.Equal("concurrent_modification", ex.Diagnostic.Code);
        Assert.Contains("Original", File.ReadAllText(Path.Combine(fixture.RootPath, "items/abc.md")));

        var record = collection.Update("items/abc.md", patch: Fm(("title", "Yes")), ifRevision: revision);
        Assert.Equal("Yes", record.Frontmatter["title"]);
    }

    // --- Delete ------------------------------------------------------------------------------

    [Fact]
    public void Delete_removes_the_file_and_the_index_entry()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Gone\n---\n");
        var collection = Connect(fixture);

        var deleted = collection.Delete("items/abc.md");

        Assert.Equal("Gone", deleted.Frontmatter["title"]);
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.False(collection.Records.ContainsKey("items/abc.md"));
    }

    [Fact]
    public void Delete_dry_run_leaves_the_file_and_index_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Stays\n---\n");
        var collection = Connect(fixture);

        collection.Delete("items/abc.md", dryRun: true);

        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.True(collection.Records.ContainsKey("items/abc.md"));
    }

    [Fact]
    public void Delete_of_missing_record_throws_record_not_found()
    {
        using var fixture = new TempCollection();
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Delete("nope.md"));
        Assert.Equal("record_not_found", ex.Diagnostic.Code);
    }

    [Fact]
    public void Update_and_Delete_reject_an_escaping_source_path_even_when_a_file_exists_there()
    {
        using var fixture = new TempCollection();
        var outside = Path.Combine(Path.GetDirectoryName(fixture.RootPath)!, "outside-" + Guid.NewGuid().ToString("N") + ".md");
        File.WriteAllText(outside, "---\ntitle: Secret\n---\n");
        try
        {
            var collection = Connect(fixture);
            var escapingPath = "../" + Path.GetFileName(outside);

            var updateEx = Assert.Throws<MdbWriteException>(() => collection.Update(escapingPath, patch: Fm(("title", "Hacked"))));
            Assert.Equal("path_traversal", updateEx.Diagnostic.Code);
            Assert.Contains("Secret", File.ReadAllText(outside));

            var deleteEx = Assert.Throws<MdbWriteException>(() => collection.Delete(escapingPath));
            Assert.Equal("path_traversal", deleteEx.Diagnostic.Code);
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    // --- Rename ------------------------------------------------------------------------------

    [Fact]
    public void Rename_moves_the_file_and_updates_the_index_without_touching_other_records()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Moving\n---\n");
        var collection = Connect(fixture);

        var record = collection.Rename("items/abc.md", "items/moved.md");

        Assert.Equal("items/moved.md", record.FileInfo.Path);
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/moved.md")));
        Assert.False(collection.Records.ContainsKey("items/abc.md"));
        Assert.True(collection.Records.ContainsKey("items/moved.md"));
    }

    [Fact]
    public void Rename_rejects_an_escaping_destination()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: X\n---\n");
        var collection = Connect(fixture);

        Assert.Throws<MdbWriteException>(() => collection.Rename("items/abc.md", "../escape.md"));
        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
    }

    [Fact]
    public void Rename_rejects_an_existing_destination_file()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: X\n---\n");
        fixture.WriteFile("items/taken.md", "---\ntype: item\nid: taken\ntitle: Y\n---\n");
        var collection = Connect(fixture);

        var ex = Assert.Throws<MdbWriteException>(() => collection.Rename("items/abc.md", "items/taken.md"));
        Assert.Equal("path_conflict", ex.Diagnostic.Code);
    }

    // --- Batch ---------------------------------------------------------------------------------

    [Fact]
    public void ExecuteBatch_default_mode_validates_every_operation_before_writing_any()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var results = collection.ExecuteBatch(new[]
        {
            MdbBatchOperation.Create(Fm(("type", "item"), ("id", "a"), ("title", "A")), path: "items/a.md"),
            MdbBatchOperation.Create(Fm(("title", "Missing required id")), path: "items/b.md"),
        });

        Assert.True(results[0].Valid);
        Assert.False(results[1].Valid);
        // whole batch aborted: neither operation actually persisted.
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/a.md")));
        Assert.Empty(collection.Records);
    }

    [Fact]
    public void ExecuteBatch_allow_partial_persists_valid_operations_despite_a_failing_one()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var results = collection.ExecuteBatch(new[]
        {
            MdbBatchOperation.Create(Fm(("type", "item"), ("id", "a"), ("title", "A")), path: "items/a.md"),
            MdbBatchOperation.Create(Fm(("title", "Missing required id")), path: "items/b.md"),
        }, allowPartial: true);

        Assert.True(results[0].Valid);
        Assert.False(results[1].Valid);
        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/a.md")));
        Assert.True(collection.Records.ContainsKey("items/a.md"));
    }

    [Fact]
    public void ExecuteBatch_same_batch_creates_targeting_the_same_generated_path_conflict()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var results = collection.ExecuteBatch(new[]
        {
            MdbBatchOperation.Create(Fm(("type", "item"), ("id", "dup"), ("title", "First")), types: new[] { "item" }),
            MdbBatchOperation.Create(Fm(("type", "item"), ("id", "dup"), ("title", "Second")), types: new[] { "item" }),
        });

        Assert.True(results[0].Valid);
        Assert.False(results[1].Valid);
        Assert.Equal("path_conflict", results[1].Diagnostics[0].Code);
    }

    [Fact]
    public void ExecuteBatch_dry_run_style_create_leaves_no_side_effects_when_validation_fails()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        collection.ExecuteBatch(new[] { MdbBatchOperation.Create(Fm(("title", "no id")), path: "items/x.md") });

        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/x.md")));
    }

    // --- Dry run -------------------------------------------------------------------------------

    [Fact]
    public void Create_dry_run_returns_the_would_be_record_without_writing_the_file()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        var record = collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "Preview")), types: new[] { "item" }, dryRun: true);

        Assert.Equal("items/abc.md", record.FileInfo.Path);
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.Empty(collection.Records);
    }

    [Fact]
    public void Update_dry_run_leaves_the_file_and_index_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Original\n---\n");
        var collection = Connect(fixture);

        var record = collection.Update("items/abc.md", patch: Fm(("title", "Would change")), dryRun: true);

        Assert.Equal("Would change", record.Frontmatter["title"]);
        Assert.Contains("Original", File.ReadAllText(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.Equal("Original", collection.Records["items/abc.md"].Frontmatter["title"]);
    }

    [Fact]
    public void Rename_dry_run_leaves_the_file_and_index_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: X\n---\n");
        var collection = Connect(fixture);

        var record = collection.Rename("items/abc.md", "items/moved.md", dryRun: true);

        Assert.Equal("items/moved.md", record.FileInfo.Path);
        Assert.True(File.Exists(Path.Combine(fixture.RootPath, "items/abc.md")));
        Assert.False(File.Exists(Path.Combine(fixture.RootPath, "items/moved.md")));
        Assert.True(collection.Records.ContainsKey("items/abc.md"));
    }

    // --- Atomic write / frontmatter round-trip -------------------------------------------------

    [Fact]
    public void Atomic_write_leaves_no_temp_file_behind_and_final_content_is_correct()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        var collection = Connect(fixture);

        collection.Create(Fm(("type", "item"), ("id", "abc"), ("title", "Final")), types: new[] { "item" });

        var entries = Directory.GetFiles(Path.Combine(fixture.RootPath, "items"));
        Assert.Single(entries);
        Assert.EndsWith("abc.md", entries[0]);
        Assert.Contains("Final", File.ReadAllText(entries[0]));
    }

    [Fact]
    public void Frontmatter_round_trip_is_stable_on_a_no_op_update()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/item.md", ItemType());
        fixture.WriteFile("items/abc.md", "---\ntype: item\nid: abc\ntitle: Stable\ncategory: books\n---\nbody\n");
        var collection = Connect(fixture);

        var before = collection.Records["items/abc.md"].Frontmatter;
        var record = collection.Update("items/abc.md", patch: Fm(("title", "Stable")));

        Assert.Equal(before["id"], record.Frontmatter["id"]);
        Assert.Equal(before["title"], record.Frontmatter["title"]);
        Assert.Equal(before["category"], record.Frontmatter["category"]);
    }
}
