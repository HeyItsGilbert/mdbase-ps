using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class DiscoveryTests
{
    [Fact]
    public void Reserved_paths_are_pruned_from_record_discovery()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", ConnectTests.TaskType());
        fixture.WriteFile("_contracts/example.md", "---\nkind: mdbase.contract\n---\n");
        fixture.WriteFile(".mdbase/state.md", "---\ntitle: derived state\n---\n");
        fixture.WriteFile("notes/kept.md", "---\ntitle: Kept\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "notes/kept.md" }, collection.Records.Keys.OrderBy(k => k));
    }

    [Fact]
    public void Nested_collection_roots_are_pruned_from_the_parent_scan()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("outer.md", "---\ntitle: Outer\n---\n");
        fixture.WriteFile("nested-project/mdbase.yaml", "spec_version: \"0.3.0\"\n");
        fixture.WriteFile("nested-project/inner.md", "---\ntitle: Inner\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "outer.md" }, collection.Records.Keys);
    }

    [Fact]
    public void Default_excludes_prune_git_and_node_modules_directories()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("kept.md", "---\ntitle: Kept\n---\n");
        fixture.WriteFile(".git/COMMIT_EDITMSG.md", "---\ntitle: Not a record\n---\n");
        fixture.WriteFile("node_modules/pkg/readme.md", "---\ntitle: Not a record\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "kept.md" }, collection.Records.Keys);
    }

    [Fact]
    public void Configured_exclude_globs_prune_matching_paths()
    {
        using var fixture = new TempCollection("""
            spec_version: "0.3.0"
            settings:
              exclude: ["drafts/**"]
            """);
        fixture.WriteFile("kept.md", "---\ntitle: Kept\n---\n");
        fixture.WriteFile("drafts/wip.md", "---\ntitle: WIP\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "kept.md" }, collection.Records.Keys);
    }

    [Fact]
    public void Include_subfolders_false_scans_only_the_collection_root()
    {
        using var fixture = new TempCollection("""
            spec_version: "0.3.0"
            settings:
              include_subfolders: false
            """);
        fixture.WriteFile("kept.md", "---\ntitle: Kept\n---\n");
        fixture.WriteFile("sub/skipped.md", "---\ntitle: Skipped\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(new[] { "kept.md" }, collection.Records.Keys);
    }
}
