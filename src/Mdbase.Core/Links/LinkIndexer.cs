using System.Collections.Specialized;
using Mdbase.Core.Compose;
using Mdbase.Core.Matching;

namespace Mdbase.Core.Links;

/// <summary>One record's outgoing link, tagged with the frontmatter field that declared it (null for a body occurrence), for backward-index insertion.</summary>
internal readonly record struct OutgoingLink(string? FieldPath, MdbLink Link);

internal sealed record LinkComputationResult(MdbRecord Record, IReadOnlyList<OutgoingLink> Outgoing);

/// <summary>
/// Phase 3's per-record orchestrator (#9; Ch.08): extracts declared frontmatter link fields
/// (via matched types' composed <see cref="LinkFieldRule"/>s) and body links/embeds/tags (via
/// <see cref="LinkParser"/>), resolves every extracted link (via <see cref="LinkResolver"/>),
/// and assembles the record's final <c>Links</c>/<c>Embeds</c>/<c>Tags</c>/<c>LinkDiagnostics</c>.
/// </summary>
internal static class LinkIndexer
{
    public static LinkComputationResult ComputeLinks(
        MdbRecord record,
        ResolutionIndexes indexes,
        IReadOnlyDictionary<string, MdbRecord> allRecords,
        string validationLevel)
    {
        var recordPath = record.FileInfo.Path;
        var diagnostics = new List<MdbDiagnostic>();
        var links = new List<MdbLink>();
        var embeds = new List<MdbLink>();
        var outgoing = new List<OutgoingLink>();

        var (linkRules, _) = TypeConflictComposer.Compose(
            record.MatchedTypes,
            type => type.LinkRules,
            EqualityComparer<LinkFieldRule>.Default,
            recordPath);

        foreach (var rule in linkRules.Values)
        {
            foreach (var (fieldPath, rawValue) in ExtractDeclaredLinkValues(rule, record.Frontmatter))
            {
                var parsed = LinkParser.ParseFrontmatterValue(rawValue);
                var resolved = LinkResolver.Resolve(parsed, recordPath, indexes, out var escapesRoot);

                links.Add(resolved);
                outgoing.Add(new OutgoingLink(fieldPath, resolved));

                AppendAmbiguityAndEscapeDiagnostics(resolved, escapesRoot, fieldPath, recordPath, diagnostics);
                AppendRuleDiagnostics(resolved, rule, fieldPath, recordPath, validationLevel, allRecords, diagnostics);
            }
        }

        var body = LinkParser.ExtractBody(record.Body);

        foreach (var raw in body.Links)
        {
            var resolved = LinkResolver.Resolve(raw, recordPath, indexes, out var escapesRoot);
            links.Add(resolved);
            outgoing.Add(new OutgoingLink(null, resolved));
            AppendAmbiguityAndEscapeDiagnostics(resolved, escapesRoot, null, recordPath, diagnostics);
        }

        foreach (var raw in body.Embeds)
        {
            var resolved = LinkResolver.Resolve(raw, recordPath, indexes, out var escapesRoot);
            embeds.Add(resolved);
            outgoing.Add(new OutgoingLink(null, resolved));
            AppendAmbiguityAndEscapeDiagnostics(resolved, escapesRoot, null, recordPath, diagnostics);
        }

        var tags = CombineTags(record.Frontmatter, body.Tags);

        var finalRecord = record with
        {
            Links = links,
            Embeds = embeds,
            Tags = tags,
            LinkDiagnostics = diagnostics,
        };

        return new LinkComputationResult(finalRecord, outgoing);
    }

    /// <summary>
    /// Ch.07 "Field References": <c>field[]</c> applies the rule to every item of that
    /// dot-path array field, keeping the declared field path (no per-item index) as its
    /// backlink field path. A JSON Pointer field applies item-wise automatically when its
    /// exactly-selected value is an array, appending the item index to the pointer.
    /// </summary>
    private static IEnumerable<(string FieldPath, string RawValue)> ExtractDeclaredLinkValues(
        LinkFieldRule rule, OrderedDictionary frontmatter)
    {
        var isDeclaredArray = rule.FieldPath.EndsWith("[]", StringComparison.Ordinal);
        var baseReference = isDeclaredArray ? rule.FieldPath[..^2] : rule.FieldPath;

        FieldRef fieldRef;
        try
        {
            fieldRef = FieldRef.Parse(baseReference);
        }
        catch (ArgumentException)
        {
            yield break;
        }

        var (exists, value) = fieldRef.Resolve(frontmatter);
        if (!exists || value is null)
        {
            yield break;
        }

        if (value is object?[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] is string s && s.Length > 0)
                {
                    var fieldPath = isDeclaredArray ? rule.FieldPath : $"{rule.FieldPath}[{i}]";
                    yield return (fieldPath, s);
                }
            }

            yield break;
        }

        if (value is string scalar && scalar.Length > 0)
        {
            yield return (rule.FieldPath, scalar);
        }
    }

    private static void AppendAmbiguityAndEscapeDiagnostics(
        MdbLink link, bool escapesRoot, string? fieldPath, string recordPath, List<MdbDiagnostic> diagnostics)
    {
        if (link.IsAmbiguous)
        {
            diagnostics.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Warning,
                Code = "ambiguous_link",
                Message = $"Link target '{link.Target}' resolves to more than one record.",
                Path = recordPath,
                Field = fieldPath,
            });

            return;
        }

        if (escapesRoot)
        {
            diagnostics.Add(new MdbDiagnostic
            {
                Severity = MdbSeverity.Warning,
                Code = "link_target_invalid",
                Message = $"Link target '{link.Target}' normalizes outside the collection root.",
                Path = recordPath,
                Field = fieldPath,
            });
        }
    }

    private static void AppendRuleDiagnostics(
        MdbLink link,
        LinkFieldRule rule,
        string fieldPath,
        string recordPath,
        string validationLevel,
        IReadOnlyDictionary<string, MdbRecord> allRecords,
        List<MdbDiagnostic> diagnostics)
    {
        if (validationLevel == "off")
        {
            return;
        }

        var severity = validationLevel == "error" ? MdbSeverity.Error : MdbSeverity.Warning;

        if (rule.ValidateExists && link.ResolvedPath is null)
        {
            diagnostics.Add(new MdbDiagnostic
            {
                Severity = severity,
                Code = "link_not_found",
                Message = $"Link field '{fieldPath}' target '{link.Target}' did not resolve to a record.",
                Path = recordPath,
                Field = fieldPath,
            });
        }

        if (rule.TargetType is not null
            && link.ResolvedPath is not null
            && allRecords.TryGetValue(link.ResolvedPath, out var targetRecord)
            && !targetRecord.MatchedTypes.Any(t => t.CanonicalName == rule.TargetType.ToLowerInvariant()))
        {
            diagnostics.Add(new MdbDiagnostic
            {
                Severity = severity,
                Code = "link_target_type_mismatch",
                Message = $"Link field '{fieldPath}' target '{link.ResolvedPath}' does not match required type '{rule.TargetType}'.",
                Path = recordPath,
                Field = fieldPath,
            });
        }
    }

    private static IReadOnlyList<string> CombineTags(OrderedDictionary frontmatter, IReadOnlyList<string> bodyTags)
    {
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddTag(string tag)
        {
            if (seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        if (frontmatter.Contains("tags"))
        {
            switch (frontmatter["tags"])
            {
                case string single:
                    AddTag(single);
                    break;
                case object?[] arr when arr.Length > 0 && arr.All(i => i is string):
                    foreach (var item in arr)
                    {
                        AddTag((string)item!);
                    }

                    break;
            }
        }

        foreach (var tag in bodyTags)
        {
            AddTag(tag);
        }

        return tags;
    }
}
