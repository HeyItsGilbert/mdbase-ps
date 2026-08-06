namespace Mdbase.Core;

/// <summary>
/// Raised by <see cref="MdbCollection.Connect"/> when the target directory has no `mdbase.yaml`
/// (spec Ch.02 "Identification"). Terminating, not an <see cref="MdbDiagnostic"/> — there is no
/// collection yet to attach diagnostics to.
/// </summary>
public sealed class MdbCollectionNotFoundException : Exception
{
    public MdbCollectionNotFoundException(string path)
        : base($"'{path}' is not an mdbase collection: no 'mdbase.yaml' was found there.")
    {
        Path = path;
    }

    public string Path { get; }
}
