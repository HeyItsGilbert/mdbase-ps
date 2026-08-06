using Mdbase.Core.Links;
using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class LinksTests
{
    [Fact]
    public void Frontmatter_wikilink_field_parses_target_alias_anchor_and_resolves_path_style()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"[[notes/b.md#section|Beta]]\"\n---\n");
        fixture.WriteFile("notes/b.md", "---\ntype: note\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["a.md"].Links);

        Assert.Equal(MdbLinkFormat.Wikilink, link.Format);
        Assert.Equal("notes/b.md", link.Target);
        Assert.Equal("Beta", link.Alias);
        Assert.Equal("section", link.Anchor);
        Assert.True(link.IsRelative);
        Assert.Equal("notes/b.md", link.ResolvedPath);
        Assert.False(link.IsAmbiguous);
    }

    [Fact]
    public void Frontmatter_markdown_link_field_resolves_relative_to_referring_folder()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"[Beta](b.md#section)\"\n---\n");
        fixture.WriteFile("notes/b.md", "---\ntype: note\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["notes/a.md"].Links);

        Assert.Equal(MdbLinkFormat.Markdown, link.Format);
        Assert.Equal("b.md", link.Target);
        Assert.Equal("Beta", link.Alias);
        Assert.Equal("section", link.Anchor);
        Assert.Equal("notes/b.md", link.ResolvedPath);
    }

    [Fact]
    public void Frontmatter_bare_path_field_beginning_with_slash_resolves_collection_root_absolute()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"/notes/b.md\"\n---\n");
        fixture.WriteFile("notes/b.md", "---\ntype: note\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["notes/a.md"].Links);

        Assert.Equal(MdbLinkFormat.Path, link.Format);
        Assert.False(link.IsRelative);
        Assert.Equal("notes/b.md", link.ResolvedPath);
    }

    [Fact]
    public void Body_extraction_finds_wikilinks_markdown_links_and_embeds_while_excluding_fenced_and_inline_code()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/b.md", "---\ntype: note\n---\n");
        fixture.WriteFile("notes/c.md", "---\ntype: note\n---\n");
        fixture.WriteFile("notes/a.md", """
            ---
            type: note
            ---
            See [[notes/b.md|Beta]] and [Gamma](notes/c.md) plus embed ![[img.png]] and image ![alt](pic.png).

            Inline code `[[notes/x.md]]` must be ignored.

            ```
            fenced [[notes/y.md]] code block must be ignored
            ```
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["notes/a.md"];

        Assert.Equal(new[] { "notes/b.md", "notes/c.md" }, record.Links.Select(l => l.Target).OrderBy(t => t));
        Assert.Equal(new[] { "img.png", "pic.png" }, record.Embeds.Select(e => e.Target).OrderBy(t => t));
        Assert.DoesNotContain(record.Links, l => l.Target.Contains("notes/x.md") || l.Target.Contains("notes/y.md"));
        Assert.DoesNotContain(record.Embeds, l => l.Target.Contains("notes/x.md") || l.Target.Contains("notes/y.md"));
    }

    [Fact]
    public void Tags_combine_frontmatter_and_body_tags_deduplicated_excluding_url_fragments()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/a.md", """
            ---
            type: note
            tags: [alpha, beta]
            ---
            #gamma at line start.

            Mid text #delta/one here, also #alpha again (dup), see https://example.com#notafragment for details.
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);
        var tags = collection.Records["notes/a.md"].Tags;

        Assert.Equal(new[] { "alpha", "beta", "gamma", "delta/one" }, tags);
        Assert.DoesNotContain("notafragment", tags);
        Assert.DoesNotContain("example.com", tags);
    }

    [Fact]
    public void Simple_wikilink_resolves_by_id_first()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/target.md", "---\ntype: note\nid: t1\n---\n");
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"[[t1]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["notes/a.md"].Links);

        Assert.Equal("notes/target.md", link.ResolvedPath);
        Assert.False(link.IsAmbiguous);
    }

    [Fact]
    public void Simple_wikilink_with_duplicate_id_is_ambiguous_and_diagnosed()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/one.md", "---\ntype: note\nid: dup\n---\n");
        fixture.WriteFile("notes/two.md", "---\ntype: note\nid: dup\n---\n");
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"[[dup]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["notes/a.md"];
        var link = Assert.Single(record.Links);

        Assert.Null(link.ResolvedPath);
        Assert.True(link.IsAmbiguous);
        Assert.Contains(record.LinkDiagnostics, d => d.Code == "ambiguous_link");
    }

    [Fact]
    public void Simple_wikilink_falls_back_to_filename_resolution_when_no_id_matches()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/unique.md", "---\ntype: note\n---\n");
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"[[unique]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["notes/a.md"].Links);

        Assert.Equal("notes/unique.md", link.ResolvedPath);
    }

    [Fact]
    public void Filename_resolution_tiebreaks_by_same_directory_first()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("notes/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("other/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("notes/a.md", "---\ntype: note\nref: \"[[dup]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["notes/a.md"].Links);

        Assert.Equal("notes/dup.md", link.ResolvedPath);
    }

    [Fact]
    public void Filename_resolution_tiebreaks_by_shortest_path_when_no_same_directory_candidate()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("deep/nested/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("shallow/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("referrer/a.md", "---\ntype: note\nref: \"[[dup]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["referrer/a.md"].Links);

        Assert.Equal("shallow/dup.md", link.ResolvedPath);
    }

    [Fact]
    public void Filename_resolution_tiebreaks_alphabetically_as_last_resort()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("bbbb/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("aaaa/dup.md", "---\ntype: note\n---\n");
        fixture.WriteFile("referrer/a.md", "---\ntype: note\nref: \"[[dup]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var link = Assert.Single(collection.Records["referrer/a.md"].Links);

        Assert.Equal("aaaa/dup.md", link.ResolvedPath);
    }

    [Fact]
    public void Link_normalizing_outside_the_collection_root_resolves_to_null_with_a_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"../outside.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];
        var link = Assert.Single(record.Links);

        Assert.Null(link.ResolvedPath);
        Assert.False(link.IsAmbiguous);
        Assert.Contains(record.LinkDiagnostics, d => d.Code == "link_target_invalid");
    }

    [Fact]
    public void Validate_exists_reports_at_the_configured_validation_level_and_off_suppresses_it()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref:\n        validate_exists: true\n"));
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"missing.md\"\n---\n");

        var errorCollection = MdbCollection.Connect(fixture.RootPath);
        var errorDiag = Assert.Single(errorCollection.Records["a.md"].LinkDiagnostics);
        Assert.Equal("link_unresolved", errorDiag.Code);
        Assert.Equal(MdbSeverity.Error, errorDiag.Severity);

        fixture.WriteFile("mdbase.yaml", "spec_version: \"0.3.0\"\nsettings:\n  validation: warn\n");
        var warnCollection = MdbCollection.Connect(fixture.RootPath);
        Assert.Equal(MdbSeverity.Warning, Assert.Single(warnCollection.Records["a.md"].LinkDiagnostics).Severity);

        fixture.WriteFile("mdbase.yaml", "spec_version: \"0.3.0\"\nsettings:\n  validation: off\n");
        var offCollection = MdbCollection.Connect(fixture.RootPath);
        Assert.Empty(offCollection.Records["a.md"].LinkDiagnostics);
    }

    [Fact]
    public void Target_type_mismatch_reports_a_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref:\n        target_type: person\n"));
        fixture.WriteFile("person.md", "---\ntype: note\n---\n");
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"person.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var diag = Assert.Single(collection.Records["a.md"].LinkDiagnostics);
        Assert.Equal("link_target_type_mismatch", diag.Code);
    }

    [Fact]
    public void Array_link_field_declared_with_bracket_suffix_applies_the_rule_item_wise()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      blocks[]:\n        validate_exists: true\n"));
        fixture.WriteFile("b1.md", "---\ntype: note\n---\n");
        fixture.WriteFile("a.md", "---\ntype: note\nblocks: [\"b1.md\", \"missing.md\"]\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.Equal(2, record.Links.Count);
        Assert.Contains(record.Links, l => l.Target == "b1.md" && l.ResolvedPath == "b1.md");
        Assert.Contains(record.Links, l => l.Target == "missing.md" && l.ResolvedPath is null);

        var diag = Assert.Single(record.LinkDiagnostics);
        Assert.Equal("blocks[]", diag.Field);
    }

    [Fact]
    public void Json_pointer_link_field_applies_item_wise_appending_the_index_to_its_backlink_field_path()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      \"/relations\":\n        validate_exists: true\n"));
        fixture.WriteFile("r1.md", "---\ntype: note\n---\n");
        fixture.WriteFile("a.md", "---\ntype: note\nrelations: [\"r1.md\", \"missing.md\"]\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["a.md"];

        Assert.Equal(2, record.Links.Count);
        Assert.Contains(record.Links, l => l.Target == "r1.md" && l.ResolvedPath == "r1.md");
        Assert.Contains(record.Links, l => l.Target == "missing.md" && l.ResolvedPath is null);

        var diag = Assert.Single(record.LinkDiagnostics);
        Assert.Equal("/relations[1]", diag.Field);

        var entry = Assert.Single(collection.GetBacklinks("r1.md"));
        Assert.Equal("/relations[0]", entry.FieldPath);
    }

    [Fact]
    public void Ambiguous_target_on_a_validate_exists_field_still_reports_link_unresolved_at_the_configured_severity()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref:\n        validate_exists: true\n"));
        fixture.WriteFile("one.md", "---\ntype: note\nid: dup\n---\n");
        fixture.WriteFile("two.md", "---\ntype: note\nid: dup\n---\n");
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"[[dup]]\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var diagnostics = collection.Records["a.md"].LinkDiagnostics;

        Assert.Contains(diagnostics, d => d.Code == "ambiguous_link" && d.Severity == MdbSeverity.Warning);
        Assert.Contains(diagnostics, d => d.Code == "link_unresolved" && d.Severity == MdbSeverity.Error);
    }

    [Fact]
    public void Refresh_of_a_record_path_does_not_retroactively_reresolve_other_records_stale_links()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("stale.md", "---\ntype: note\nref: \"target.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Null(Assert.Single(collection.Records["stale.md"].Links).ResolvedPath);

        // The target now exists, but only source.md (not stale.md) is refreshed.
        fixture.WriteFile("target.md", "---\ntype: note\n---\n");
        fixture.WriteFile("source.md", "---\ntype: note\nref: \"target.md\"\n---\n");
        collection.Refresh("target.md");
        collection.Refresh("source.md");

        Assert.Null(Assert.Single(collection.Records["stale.md"].Links).ResolvedPath);
        Assert.Equal("target.md", Assert.Single(collection.Records["source.md"].Links).ResolvedPath);
    }

    [Fact]
    public void Identical_links_rules_across_matched_types_coalesce_without_a_composition_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", NoteType("      ref:\n        validate_exists: true\n", typeName: "a"));
        fixture.WriteFile("_types/b.md", NoteType("      ref:\n        validate_exists: true\n", typeName: "b"));
        fixture.WriteFile("record.md", "---\ntypes: [a, b]\nref: \"missing.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["record.md"];

        Assert.Empty(record.CompositionDiagnostics);
        Assert.Single(record.LinkDiagnostics);
    }

    [Fact]
    public void Differing_links_rules_across_matched_types_report_a_type_conflict()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/a.md", NoteType("      ref:\n        validate_exists: true\n", typeName: "a"));
        fixture.WriteFile("_types/b.md", NoteType("      ref:\n        validate_exists: false\n", typeName: "b"));
        fixture.WriteFile("record.md", "---\ntypes: [a, b]\nref: \"missing.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["record.md"];

        var conflict = Assert.Single(record.CompositionDiagnostics);
        Assert.Equal("type_conflict", conflict.Code);
        Assert.Equal("ref", conflict.Field);
        // Neither type's rule applies once conflicted, so no validate_exists diagnostic fires.
        Assert.Empty(record.LinkDiagnostics);
    }

    [Fact]
    public void Backward_index_only_contains_resolved_non_ambiguous_links()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("target.md", "---\ntype: note\n---\n");
        fixture.WriteFile("resolved.md", "---\ntype: note\nref: \"target.md\"\n---\n");
        fixture.WriteFile("unresolved.md", "---\ntype: note\nref: \"missing.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var backlinks = collection.GetBacklinks("target.md");
        var entry = Assert.Single(backlinks);
        Assert.Equal("resolved.md", entry.SourcePath);
        Assert.Equal("ref", entry.FieldPath);

        Assert.Empty(collection.GetBacklinks("missing.md"));
    }

    [Fact]
    public void Refresh_of_a_record_path_removes_stale_backlinks_and_inserts_new_ones()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("target-one.md", "---\ntype: note\n---\n");
        fixture.WriteFile("target-two.md", "---\ntype: note\n---\n");
        fixture.WriteFile("source.md", "---\ntype: note\nref: \"target-one.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Single(collection.GetBacklinks("target-one.md"));
        Assert.Empty(collection.GetBacklinks("target-two.md"));

        fixture.WriteFile("source.md", "---\ntype: note\nref: \"target-two.md\"\n---\n");
        collection.Refresh("source.md");

        Assert.Empty(collection.GetBacklinks("target-one.md"));
        var entry = Assert.Single(collection.GetBacklinks("target-two.md"));
        Assert.Equal("source.md", entry.SourcePath);
    }

    [Fact]
    public void Refresh_of_a_new_record_validates_target_type_against_its_new_snapshot()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref:\n        target_type: person\n"));
        fixture.WriteFile("_types/person.md", NoteType(string.Empty, typeName: "person"));

        var collection = MdbCollection.Connect(fixture.RootPath);
        fixture.WriteFile("self.md", "---\ntype: note\nref: self.md\n---\n");

        collection.Refresh("self.md");

        var diagnostic = Assert.Single(collection.Records["self.md"].LinkDiagnostics);
        Assert.Equal("link_target_type_mismatch", diagnostic.Code);
        Assert.Equal(MdbSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Refresh_of_a_type_path_rebuilds_link_diagnostics_for_every_record()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/note.md", NoteType("      ref: {}\n"));
        fixture.WriteFile("a.md", "---\ntype: note\nref: \"missing.md\"\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Empty(collection.Records["a.md"].LinkDiagnostics);

        fixture.WriteFile("_types/note.md", NoteType("      ref:\n        validate_exists: true\n"));
        collection.Refresh("_types/note.md");

        var diag = Assert.Single(collection.Records["a.md"].LinkDiagnostics);
        Assert.Equal("link_unresolved", diag.Code);
    }

    private static string NoteType(string linksYaml, string typeName = "note") => $"""
        ---
        kind: mdbase.type
        name: {typeName}

        match:
          path_glob: "**/*.md"

        schema:
          dialect: json-schema-2020-12
          value:
            type: object
            additionalProperties: true

        collection:
          links:
        {linksYaml}
        ---
        """;
}
