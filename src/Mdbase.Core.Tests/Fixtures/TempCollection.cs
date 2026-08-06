namespace Mdbase.Core.Tests.Fixtures;

/// <summary>
/// Scaffolds a real temp-directory mdbase collection for a test (no mocking of the
/// filesystem/YamlDotNet/JsonSchema.Net, per #14/#30's testing policy). Deletes itself on
/// disposal.
/// </summary>
public sealed class TempCollection : IDisposable
{
    public string RootPath { get; }

    public TempCollection(string? configYaml = null)
    {
        RootPath = Path.Combine(Path.GetTempPath(), "mdbase-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        WriteFile("mdbase.yaml", configYaml ?? "spec_version: \"0.3.0\"\n");
    }

    /// <summary>Writes a file at <paramref name="relativePath"/>, creating intermediate folders as needed.</summary>
    public TempCollection WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return this;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
