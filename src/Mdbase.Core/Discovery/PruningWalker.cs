namespace Mdbase.Core.Discovery;

/// <summary>
/// A stack-based recursive walker (#8 resolution point 2) that prunes at excluded
/// subtrees as it descends, instead of enumerating a whole tree and filtering afterward like
/// <c>Directory.EnumerateFiles(..., SearchOption.AllDirectories)</c> would — it never wastes
/// I/O walking into an excluded or nested-collection subtree.
/// </summary>
internal static class PruningWalker
{
    /// <summary>
    /// Walks every file under <paramref name="startAbsoluteDir"/>. <paramref name="pruneDirectory"/>
    /// receives each subdirectory's collection-relative path and returns true to skip descending
    /// into it.
    /// </summary>
    public static IEnumerable<string> WalkFiles(
        string collectionRoot, string startAbsoluteDir, Func<string, bool> pruneDirectory)
    {
        if (!Directory.Exists(startAbsoluteDir))
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(startAbsoluteDir);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                if (Directory.Exists(entry))
                {
                    var relative = PathUtil.ToRelative(collectionRoot, entry);
                    if (!pruneDirectory(relative))
                    {
                        stack.Push(entry);
                    }
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        return files;
    }
}
