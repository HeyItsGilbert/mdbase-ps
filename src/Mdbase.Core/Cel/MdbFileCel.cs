namespace Mdbase.Core.Cel;

/// <summary>
/// The CEL-facing <c>file</c> struct (spec Ch.03 "File Metadata"): registered with Celly's
/// <c>NativeTypeProvider</c> for struct identity/typed field access/<c>has()</c>/equality.
/// <see cref="Size"/>/<see cref="Mtime"/>/<see cref="Ctime"/> are sourced from the live
/// filesystem entry at evaluation time — deliberately not persisted on <see cref="MdbRecord"/>,
/// which stays free of mutable/environment-dependent data (#37).
/// </summary>
internal sealed class MdbFileCel
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string Basename { get; init; }
    public required string Ext { get; init; }
    public required string Folder { get; init; }
    public required long Size { get; init; }
    public required DateTimeOffset Mtime { get; init; }
    public required DateTimeOffset Ctime { get; init; }
    public required string Body { get; init; }

    /// <summary>`file.inFolder(path)` (Ch.10 "File And Link Helpers"): true when this file's folder is <paramref name="folder"/> or a descendant of it.</summary>
    public bool InFolder(string folder)
    {
        var normalized = folder.Trim('/');
        return normalized.Length == 0
            ? Folder.Length > 0
            : Folder == normalized || Folder.StartsWith(normalized + "/", StringComparison.Ordinal);
    }

    public static MdbFileCel Build(string collectionRoot, string relativePath, string body)
    {
        var absolutePath = System.IO.Path.Combine(collectionRoot, relativePath);
        var name = relativePath.Length == 0 ? string.Empty : relativePath[(relativePath.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        var basename = dot > 0 ? name[..dot] : name;
        var ext = dot > 0 ? name[(dot + 1)..] : string.Empty;
        var slash = relativePath.LastIndexOf('/');
        var folder = slash < 0 ? string.Empty : relativePath[..slash];

        long size = 0;
        var mtime = DateTimeOffset.UnixEpoch;
        var ctime = DateTimeOffset.UnixEpoch;
        if (File.Exists(absolutePath))
        {
            var info = new FileInfo(absolutePath);
            size = info.Length;
            mtime = info.LastWriteTimeUtc;
            ctime = info.CreationTimeUtc;
        }

        return new MdbFileCel
        {
            Path = relativePath, Name = name, Basename = basename, Ext = ext, Folder = folder,
            Size = size, Mtime = mtime, Ctime = ctime, Body = body,
        };
    }
}
