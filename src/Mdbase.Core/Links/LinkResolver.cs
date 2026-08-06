namespace Mdbase.Core.Links;

/// <summary>
/// Pure Ch.08 "Resolution"/"Ambiguity" functions (#9 point 4): no I/O, resolves one
/// <see cref="MdbLink"/> against the referring record's path and the current
/// <see cref="ResolutionIndexes"/>. Never throws — an unresolved, ambiguous, or
/// root-escaping target simply comes back with <see cref="MdbLink.ResolvedPath"/> null.
/// </summary>
internal static class LinkResolver
{
    /// <summary>
    /// Resolves <paramref name="link"/> as seen from <paramref name="referrerPath"/>.
    /// <paramref name="escapesRoot"/> is true only when the target normalizes outside the
    /// collection root (Ch.08: "a link that escapes the collection root is invalid").
    /// </summary>
    public static MdbLink Resolve(MdbLink link, string referrerPath, ResolutionIndexes indexes, out bool escapesRoot)
    {
        escapesRoot = false;

        var usesPathStyleResolution = link.Format != MdbLinkFormat.Wikilink || ContainsPathSeparator(link.Target);
        if (usesPathStyleResolution)
        {
            var normalized = NormalizePath(link.Target, referrerPath);
            if (normalized is null)
            {
                escapesRoot = true;
                return link with { ResolvedPath = null, IsAmbiguous = false };
            }

            var resolved = indexes.AllPaths.Contains(normalized) ? normalized : null;
            return link with { ResolvedPath = resolved, IsAmbiguous = false };
        }

        // Simple wikilink, no path separators: ID-based resolution first, then filename (Ch.08 "General Rules").
        var (idPath, idAmbiguous) = indexes.ResolveId(link.Target);
        if (idAmbiguous)
        {
            return link with { ResolvedPath = null, IsAmbiguous = true };
        }

        if (idPath is not null)
        {
            return link with { ResolvedPath = idPath, IsAmbiguous = false };
        }

        var candidates = indexes.GetBasenameCandidates(link.Target);
        var (winner, filenameAmbiguous) = TieBreak(candidates, referrerPath);
        return link with { ResolvedPath = winner, IsAmbiguous = filenameAmbiguous };
    }

    private static bool ContainsPathSeparator(string target) =>
        target.Contains('/') || target == "." || target == "..";

    /// <summary>
    /// Normalizes <paramref name="target"/> relative to <paramref name="referrerPath"/>'s folder
    /// (or as collection-root-absolute when it begins with `/`) into a collection-relative path.
    /// Returns null when the normalized path would escape the collection root.
    /// </summary>
    private static string? NormalizePath(string target, string referrerPath)
    {
        var combined = target.StartsWith('/')
            ? target.TrimStart('/')
            : Combine(GetDirectory(referrerPath), target);

        var segments = new List<string>();
        foreach (var segment in combined.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static string Combine(string directory, string target) =>
        directory.Length == 0 ? target : directory + "/" + target;

    private static string GetDirectory(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    /// <summary>Ch.08 "Ambiguity" stable tiebreak order: same directory, then shortest path, then alphabetical.</summary>
    private static (string? Winner, bool Ambiguous) TieBreak(IReadOnlyList<string> candidates, string referrerPath)
    {
        if (candidates.Count == 0)
        {
            return (null, false);
        }

        if (candidates.Count == 1)
        {
            return (candidates[0], false);
        }

        var pool = candidates;

        var referrerDirectory = GetDirectory(referrerPath);
        var sameDirectory = pool.Where(p => GetDirectory(p) == referrerDirectory).ToList();
        if (sameDirectory.Count == 1)
        {
            return (sameDirectory[0], false);
        }

        if (sameDirectory.Count > 1)
        {
            pool = sameDirectory;
        }

        var shortestLength = pool.Min(p => p.Length);
        var shortest = pool.Where(p => p.Length == shortestLength).ToList();
        if (shortest.Count == 1)
        {
            return (shortest[0], false);
        }

        if (shortest.Count > 1)
        {
            pool = shortest;
        }

        var alphabeticallyFirst = pool.OrderBy(p => p, StringComparer.Ordinal).First();
        var tied = pool.Where(p => string.Equals(p, alphabeticallyFirst, StringComparison.Ordinal)).ToList();

        return tied.Count == 1 ? (tied[0], false) : (null, true);
    }
}
