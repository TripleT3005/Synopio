using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Synopio.Engine.Text;

public static partial class PreparedText
{
    // Methods
    public static string Format(IEnumerable<Paragraph> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);

        var builder = new StringBuilder();
        int? currentPage = null;

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.PageNumber is { } page && page != currentPage)
            {
                builder.Append(CultureInfo.InvariantCulture, $"[[{page}]]").Append('\n').Append('\n');
                currentPage = page;
            }

            builder.Append(paragraph.Text).Append('\n').Append('\n');
        }

        return builder.ToString().TrimEnd('\n') + "\n";
    }

    public static List<Paragraph> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var paragraphs = new List<Paragraph>();
        int? page = null;

        foreach (var block in BlankLinePattern().Split(text.Replace("\r\n", "\n", StringComparison.Ordinal)))
        {
            var lines = block.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            var content = new List<string>();

            foreach (var line in lines)
            {
                var marker = PageMarkerPattern().Match(line);
                if (marker.Success)
                {
                    AddParagraph(content, page, paragraphs);
                    page = int.Parse(marker.Groups[1].Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    content.Add(line);
                }
            }

            AddParagraph(content, page, paragraphs);
        }

        return paragraphs;
    }

    private static void AddParagraph(List<string> content, int? page, List<Paragraph> paragraphs)
    {
        if (content.Count > 0)
        {
            paragraphs.Add(new Paragraph(string.Join(' ', content), page));
            content.Clear();
        }
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLinePattern();

    [GeneratedRegex(@"^\[\[(\d{1,5})\]\]$")]
    private static partial Regex PageMarkerPattern();
}
