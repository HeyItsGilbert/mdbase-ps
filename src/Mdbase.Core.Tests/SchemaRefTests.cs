using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class SchemaRefTests
{
    [Fact]
    public void Schema_ref_loads_and_compiles_an_external_local_json_schema_file()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.schema.json", """
            {
              "type": "object",
              "required": ["title"],
              "properties": { "title": { "type": "string" } }
            }
            """);
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task

            match:
              path_glob: "*.md"

            schema:
              dialect: json-schema-2020-12
              ref: "task.schema.json"
            ---
            """);
        fixture.WriteFile("record.md", "---\ntitle: Has title\n---\n");
        fixture.WriteFile("missing-title.md", "---\nother: 1\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Single(collection.Types);
        Assert.True(collection.Records["record.md"].IsValid);
        Assert.False(collection.Records["missing-title.md"].IsValid);
    }

    [Fact]
    public void Schema_ref_escaping_the_collection_root_is_rejected_with_schema_ref_forbidden()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            schema:
              dialect: json-schema-2020-12
              ref: "../../../etc/passwd"
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "schema_ref_forbidden");
    }

    [Fact]
    public void A_type_declaring_both_schema_value_and_ref_is_invalid()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/both.md", """
            ---
            kind: mdbase.type
            name: both
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
              ref: "does-not-matter.json"
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, d => d.Code == "type_invalid");
    }
}
