namespace Mdbase.Core.Discovery;

/// <summary>Collection-relative, forward-slash path helpers (spec Ch.02 "Paths And Safety").</summary>
internal static class PathUtil
{
    /// <summary>Converts an absolute filesystem path under <paramref name="root"/> into a collection-relative, forward-slash path.</summary>
    public static string ToRelative(string root, string absolutePath)
    {
        var relative = Path.GetRelativePath(root, absolutePath);
        return relative.Replace('\\', '/');
    }
}
