using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class RefreshTests
{
    [Fact]
    public void Refresh_of_a_record_path_re_derives_just_that_record()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", ConnectTests.TaskType());
        fixture.WriteFile("tasks/a.md", "---\ntype: task\ntitle: Original\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Equal("Original", collection.Records["tasks/a.md"].Frontmatter["title"]);

        fixture.WriteFile("tasks/a.md", "---\ntype: task\ntitle: Updated\n---\n");
        collection.Refresh("tasks/a.md");

        Assert.Equal("Updated", collection.Records["tasks/a.md"].Frontmatter["title"]);
    }

    [Fact]
    public void Refresh_of_a_deleted_record_path_removes_it_from_the_index()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("record.md", "---\ntitle: Gone soon\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.True(collection.Records.ContainsKey("record.md"));

        File.Delete(Path.Combine(fixture.RootPath, "record.md"));
        collection.Refresh("record.md");

        Assert.False(collection.Records.ContainsKey("record.md"));
    }

    [Fact]
    public void Refresh_of_a_type_path_rebuilds_the_registry_and_rematches_existing_records()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("notes/candidate.md", "---\ntitle: Candidate\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Empty(collection.Records["notes/candidate.md"].MatchedTypes);

        fixture.WriteFile("_types/note.md", MatchingTests.TypeWithSchema("note", "\"notes/**/*.md\"", requireField: null));
        collection.Refresh("_types/note.md");

        Assert.Equal(new[] { "note" }, collection.Records["notes/candidate.md"].MatchedTypes.Select(t => t.Name));
    }
}
