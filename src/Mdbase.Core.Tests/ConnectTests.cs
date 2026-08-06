using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class ConnectTests
{
    [Fact]
    public void Connect_throws_when_directory_has_no_mdbase_yaml()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mdbase-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<MdbCollectionNotFoundException>(() => MdbCollection.Connect(dir));
            Assert.Contains("mdbase.yaml", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Connect_succeeds_for_a_minimal_collection_with_only_mdbase_yaml()
    {
        using var fixture = new TempCollection();

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Empty(collection.Records);
        Assert.Equal("0.3.0", collection.Config.SpecVersion);
    }

    [Fact]
    public void Connect_loads_a_valid_single_type_record()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskType());
        fixture.WriteFile("tasks/fix-login.md", "---\ntype: task\ntitle: Fix login\n---\nReproduce and fix.\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var record = collection.Records["tasks/fix-login.md"];
        Assert.True(record.IsValid);
        Assert.Empty(record.ValidationDiagnostics);
        Assert.Equal(new[] { "task" }, record.MatchedTypes.Select(t => t.Name));
        Assert.StartsWith("sha256:", record.Revision);
        Assert.Equal("Reproduce and fix.\n", record.Body);
    }

    [Fact]
    public void Connect_loads_an_untyped_record_matching_zero_types_successfully()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskType());
        fixture.WriteFile("notes/random.md", "---\ntitle: Just a note\n---\nNo type here.\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var record = collection.Records["notes/random.md"];
        Assert.Empty(record.MatchedTypes);
        Assert.True(record.IsValid);
    }

    [Fact]
    public void Record_paths_are_forward_slash_normalized_and_derive_directory_and_name()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("notes/nested/deep/entry.md", "---\ntitle: Deep\n---\nBody\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var record = collection.Records["notes/nested/deep/entry.md"];
        Assert.Equal("notes/nested/deep/entry.md", record.FileInfo.Path);
        Assert.Equal("notes/nested/deep", record.FileInfo.Directory);
        Assert.Equal("entry.md", record.FileInfo.Name);
    }

    internal static string TaskType() => """
        ---
        kind: mdbase.type
        name: task
        version: 1

        match:
          path_glob: "tasks/**/*.md"

        schema:
          dialect: json-schema-2020-12
          value:
            type: object
            required: [title]
            additionalProperties: false
            properties:
              type:
                const: task
              title:
                type: string
                minLength: 1
              status:
                type: string

        collection:
          read_defaults:
            status: open
        ---

        # Task
        """;
}
