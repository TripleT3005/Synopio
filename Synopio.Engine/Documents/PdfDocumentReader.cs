using System.Globalization;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Synopio.Engine.Documents;

public sealed class PdfDocumentReader : IDocumentReader
{
    // Constants
    private const double BaselineTolerance = 0.35;
    private const double SpaceGapFactor = 0.2;
    private const double DescentFactor = 0.25;
    private const char SoftHyphen = '\u00AD';
    private const char UnknownCharacter = '\uFFFD';

    // Properties
    public IReadOnlyCollection<string> Extensions { get; } = [".pdf"];

    // Methods
    public SourceDocument Read(Stream stream, string fileName, string sha256)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<SourceLine>();

        foreach (var page in document.GetPages())
        {
            lines.AddRange(ReadPage(page));
        }

        return new SourceDocument
        {
            FileName = fileName,
            Sha256 = sha256,
            HasLayout = true,
            Lines = lines,
        };
    }

    private static IEnumerable<SourceLine> ReadPage(Page page)
    {
        var letters = page.Letters
            .Where(letter => letter.TextOrientation == TextOrientation.Horizontal && letter.Value.Length > 0)
            .OrderByDescending(letter => letter.StartBaseLine.Y)
            .ThenBy(letter => letter.StartBaseLine.X)
            .ToList();

        var groups = new List<List<Letter>>();
        foreach (var letter in letters)
        {
            var group = groups.LastOrDefault();
            if (group != null && Math.Abs(group[0].StartBaseLine.Y - letter.StartBaseLine.Y) <= Math.Max(1.0, group[0].PointSize * BaselineTolerance))
            {
                group.Add(letter);
            }
            else
            {
                groups.Add([letter]);
            }
        }

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(letter => letter.StartBaseLine.X).ToList();
            var text = BuildText(ordered);
            if (text.Length == 0)
            {
                continue;
            }

            var visible = ordered.Where(letter => !string.IsNullOrWhiteSpace(letter.Value)).ToList();
            var baseline = page.Height - visible[0].StartBaseLine.Y;
            var fontSize = visible.Max(letter => letter.PointSize);
            yield return new SourceLine(text, page.Number)
            {
                Top = baseline - fontSize,
                Bottom = baseline + (fontSize * DescentFactor),
                Left = visible.Min(letter => letter.BoundingBox.Left),
                Right = visible.Max(letter => letter.BoundingBox.Right),
                FontSize = fontSize,
            };
        }
    }

    private static string BuildText(List<Letter> letters)
    {
        var builder = new StringBuilder();
        Letter? previous = null;
        Letter? whitespace = null;

        for (var index = 0; index < letters.Count; index++)
        {
            var letter = letters[index];
            if (string.IsNullOrWhiteSpace(letter.Value))
            {
                whitespace = letter;
                continue;
            }

            if (letter.Value[0] == SoftHyphen && index < letters.Count - 1)
            {
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(letter.Value[0]) == UnicodeCategory.NonSpacingMark)
            {
                builder.Append(letter.Value);
                continue;
            }

            if (char.IsControl(letter.Value[0]) || letter.Value[0] == UnknownCharacter)
            {
                if (builder.Length > 0 && builder[^1] != UnknownCharacter)
                {
                    builder.Append(UnknownCharacter);
                }

                continue;
            }

            if (previous != null)
            {
                var advancesAfterWhitespace = whitespace != null
                    && whitespace.StartBaseLine.X >= previous.EndBaseLine.X - (whitespace.Width / 2)
                    && letter.StartBaseLine.X >= whitespace.StartBaseLine.X + (whitespace.Width / 2);
                var hasGap = letter.StartBaseLine.X - previous.EndBaseLine.X > letter.PointSize * SpaceGapFactor;
                if (advancesAfterWhitespace || hasGap)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(letter.Value);
            previous = letter;
            whitespace = null;
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
