using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class AdditionalCoverageTests
{
    [Fact]
    public void A_markdown_file_under_the_types_folder_without_kind_mdbase_type_is_not_a_type()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/README.md", "---\ntitle: Just documentation\n---\nNot a type file.\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void A_malformed_embedded_json_schema_fails_fast_with_a_type_invalid_diagnostic()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/broken-schema.md", """
            ---
            kind: mdbase.type
            name: broken
            schema:
              dialect: json-schema-2020-12
              value:
                type: not-a-real-json-schema-type
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_invalid" && d.Path == "_types/broken-schema.md");
    }

    [Fact]
    public void A_nested_collection_root_under_the_types_folder_is_pruned_from_type_discovery()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/vendored-pack/mdbase.yaml", "spec_version: \"0.3.0\"\n");
        fixture.WriteFile("_types/vendored-pack/foreign.md", """
            ---
            kind: mdbase.type
            name: foreign
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
    }

    [Fact]
    public void Refresh_produces_a_new_record_instance_leaving_a_previously_held_reference_untouched()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("record.md", "---\ntitle: Original\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);
        var original = collection.Records["record.md"];

        fixture.WriteFile("record.md", "---\ntitle: Changed\n---\n");
        collection.Refresh("record.md");

        Assert.Equal("Original", original.Frontmatter["title"]);
        Assert.NotSame(original, collection.Records["record.md"]);
        Assert.Equal("Changed", collection.Records["record.md"].Frontmatter["title"]);
    }
}
