using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mdbase.Core.Write;

/// <summary>
/// Native C# delegates for the seven standard lifecycle value providers (spec Ch.09 "Standard
/// Value Providers"; #11 point 2) — never desugared into CEL, since `slugify`/`copy` need
/// direct mutable-draft access CEL can't cheaply provide.
/// </summary>
internal static class LifecycleProviders
{
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static object? Evaluate(MdbLifecycleProviderKind kind, object? arg, OrderedDictionary draft) => kind switch
    {
        MdbLifecycleProviderKind.Now => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        MdbLifecycleProviderKind.Today => DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        MdbLifecycleProviderKind.Uuid => Guid.NewGuid().ToString(),
        MdbLifecycleProviderKind.Ulid => Ulid.NewUlid(),
        MdbLifecycleProviderKind.Slugify => Slugify(ReadSourceField(draft, (string)arg!)),
        MdbLifecycleProviderKind.Copy => draft.Contains((string)arg!) ? draft[(string)arg!] : null,
        MdbLifecycleProviderKind.Literal => arg,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lifecycle provider kind."),
    };

    private static string? ReadSourceField(OrderedDictionary draft, string field) =>
        draft.Contains(field) ? draft[field] as string ?? draft[field]?.ToString() : null;

    private static string Slugify(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lowered = value.Trim().ToLowerInvariant();
        var slug = NonSlugChars.Replace(lowered, "-").Trim('-');
        return slug;
    }
}
