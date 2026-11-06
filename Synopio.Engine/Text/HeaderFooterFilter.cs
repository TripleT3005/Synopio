using System.Text.RegularExpressions;
using Synopio.Engine.Documents;

namespace Synopio.Engine.Text;

internal static partial class HeaderFooterFilter
{
    // Constants
    private const int ZoneLineCount = 3;
    private const double ZoneShare = 0.1;
    private const int MinimumRepetitions = 3;
    private const double MinimumPageShare = 0.05;

    // Methods
    public static List<SourceLine> Apply(IReadOnlyList<SourceLine> lines)
    {
        var pages = lines.GroupBy(line => line.PageNumber).ToList();
        if (pages.Count < 2)
        {
            return [.. lines];
        }

        var zoneLines = new HashSet<SourceLine>(ReferenceEqualityComparer.Instance);
        var pageCountByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasLayout = lines.Any(line => line.Bottom > 0);
        var top = hasLayout ? lines.Min(line => line.Top) : 0;
        var bottom = hasLayout ? lines.Max(line => line.Bottom) : 0;
        var margin = (bottom - top) * ZoneShare;

        foreach (var page in pages)
        {
            var pageLines = page.ToList();
            var zone = hasLayout
                ? pageLines.Where(line => line.Top <= top + margin || line.Bottom >= bottom - margin).ToList()
                : pageLines.Take(ZoneLineCount).Concat(pageLines.Skip(Math.Max(ZoneLineCount, pageLines.Count - ZoneLineCount))).ToList();
            foreach (var line in zone)
            {
                zoneLines.Add(line);
            }

            foreach (var key in zone.Select(line => CreateKey(line.Text)).Distinct())
            {
                pageCountByKey[key] = pageCountByKey.GetValueOrDefault(key) + 1;
            }
        }

        var threshold = pages.Count < MinimumRepetitions ? pages.Count : Math.Max(MinimumRepetitions, (int)Math.Ceiling(pages.Count * MinimumPageShare));
        return [.. lines.Where(line => !zoneLines.Contains(line) || !IsRunningLine(line.Text, pageCountByKey, threshold))];
    }

    private static bool IsRunningLine(string text, Dictionary<string, int> pageCountByKey, int threshold) =>
        PageNumberPattern().IsMatch(text) || pageCountByKey.GetValueOrDefault(CreateKey(text)) >= threshold;

    internal static string CreateKey(string text) =>
        WhitespacePattern().Replace(DigitPattern().Replace(text, "#"), " ").Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[\s\-–—|]*((seite|page|s\.)\s*\|?\s*)?\d{1,4}(\s*(von|of|/)\s*\d{1,4})?[\s\-–—|]*$", RegexOptions.IgnoreCase)]
    private static partial Regex PageNumberPattern();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
