using System.Text;
using System.Text.RegularExpressions;

namespace Mdbase.Core.Matching;

/// <summary>
/// Compiles an mdbase path glob (`*` within one segment, `**` across segments, `?` one
/// character) to a <see cref="Regex"/> matched against a collection-relative, forward-slash
/// path. Used by both `match.path_glob` (spec Ch.07) and `settings.exclude` (spec Ch.02).
/// </summary>
internal static class GlobPattern
{
    public static Regex Compile(string pattern)
    {
        var sb = new StringBuilder("^");
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    if (i + 2 < pattern.Length && pattern[i + 2] == '/')
                    {
                        // "**/" matches zero or more whole path segments, including none —
                        // "tasks/**/*.md" must still match "tasks/a.md", not just "tasks/x/a.md".
                        sb.Append("(?:.*/)?");
                        i += 3;
                    }
                    else
                    {
                        sb.Append(".*");
                        i += 2;
                    }
                }
                else
                {
                    sb.Append("[^/]*");
                    i += 1;
                }
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
                i += 1;
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
                i += 1;
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.Compiled);
    }
}
