using System.Text;
using System.Text.RegularExpressions;
using Synopio.Engine.Documents;

namespace Synopio.Engine.Text;

internal static partial class ParagraphBuilder
{
    // Constants
    private const double GapFactor = 1.3;
    private const double ShortLineShare = 0.8;
    private const double VeryShortLineShare = 0.6;
    private const double FontSizeTolerance = 0.9;

    // Fields
    private static readonly HashSet<string> WordsAfterSuspendedHyphen = new(StringComparer.Ordinal)
    {
        "und", "oder", "bzw", "sowie", "bis", "als", "and", "or", "to",
    };

    // Methods
    public static List<Paragraph> Build(IReadOnlyList<SourceLine> lines, bool hasLayout, bool joinHyphenatedWords)
    {
        var paragraphs = new List<Paragraph>();
        if (lines.Count == 0)
        {
            return paragraphs;
        }

        var lineSpacing = hasLayout ? TypicalLineSpacing(lines) : 0;
        var typicalRight = TypicalRight(lines);
        var bodyFontSize = hasLayout ? TypicalFontSize(lines) : 0;

        var builder = new StringBuilder();
        int? page = null;
        SourceLine? previous = null;

        foreach (var line in lines)
        {
            if (previous != null && StartsNewParagraph(previous, line, hasLayout, lineSpacing, typicalRight, bodyFontSize))
            {
                Flush();
            }

            if (builder.Length == 0)
            {
                page = line.PageNumber;
                builder.Append(line.Text);
            }
            else
            {
                AppendLine(builder, line.Text, joinHyphenatedWords);
            }

            previous = line;
        }

        Flush();
        return paragraphs;

        void Flush()
        {
            var text = builder.ToString().Trim();
            if (text.Length > 0)
            {
                paragraphs.Add(new Paragraph(text, page));
            }

            builder.Clear();
        }
    }

    private static bool StartsNewParagraph(SourceLine previous, SourceLine line, bool hasLayout, double lineSpacing, double typicalRight, double bodyFontSize)
    {
        if (line.StartsParagraph || StructureMarkerPattern().IsMatch(line.Text))
        {
            return true;
        }

        var previousText = previous.Text.TrimEnd();
        var endsSentence = previousText.Length > 0 && previousText[^1] is '.' or ':' or '!' or '?' or ';';
        var isShort = previous.Right < typicalRight * ShortLineShare;

        if (endsSentence && isShort)
        {
            return true;
        }

        if (previous.Right < typicalRight * VeryShortLineShare && line.Text.Length > 0 && !char.IsLower(line.Text[0]))
        {
            return true;
        }

        if (!hasLayout)
        {
            return false;
        }

        if (line.PageNumber != previous.PageNumber)
        {
            return endsSentence && line.Text.Length > 0 && !char.IsLower(line.Text[0]);
        }

        var gap = line.Top - previous.Top;
        if (lineSpacing > 0 && gap > lineSpacing * GapFactor)
        {
            return true;
        }

        var isHeading = previous.FontSize > bodyFontSize / FontSizeTolerance || line.FontSize > bodyFontSize / FontSizeTolerance;
        return Math.Abs(previous.FontSize - line.FontSize) > 0.5 && isHeading;
    }

    private static void AppendLine(StringBuilder builder, string text, bool joinHyphenatedWords)
    {
        var length = builder.Length;
        var endsWithHyphen = length >= 2 && builder[length - 1] is '-' or '\u00AD' && char.IsLetter(builder[length - 2]);

        if (!endsWithHyphen || !joinHyphenatedWords)
        {
            builder.Append(' ').Append(text);
            return;
        }

        var firstWord = FirstWordPattern().Match(text).Value;
        if (firstWord.Length > 0 && char.IsLower(firstWord[0]) && !WordsAfterSuspendedHyphen.Contains(firstWord))
        {
            builder.Length--;
            builder.Append(text);
        }
        else if (firstWord.Length > 0 && !WordsAfterSuspendedHyphen.Contains(firstWord))
        {
            builder.Append(text);
        }
        else
        {
            builder.Append(' ').Append(text);
        }
    }

    private static double TypicalLineSpacing(IReadOnlyList<SourceLine> lines)
    {
        var gaps = new List<double>();
        for (var index = 1; index < lines.Count; index++)
        {
            if (lines[index].PageNumber == lines[index - 1].PageNumber)
            {
                var gap = lines[index].Top - lines[index - 1].Top;
                if (gap > 0)
                {
                    gaps.Add(gap);
                }
            }
        }

        return Median(gaps);
    }

    private static double TypicalRight(IReadOnlyList<SourceLine> lines)
    {
        var rights = lines.Select(line => line.Right).OrderBy(value => value).ToList();
        return rights.Count == 0 ? 0 : rights[(int)(rights.Count * 0.9)];
    }

    private static double TypicalFontSize(IReadOnlyList<SourceLine> lines) =>
        lines.GroupBy(line => Math.Round(line.FontSize, 1)).MaxBy(group => group.Sum(line => line.Text.Length))?.Key ?? 0;

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        return values[(values.Count - 1) / 2];
    }

    [GeneratedRegex(@"^(§+\s*\d|Art(ikel|\.)\s*\d|Article\s+\d|\(\d+[a-z]?\)\s|\d+[a-z]?\.\s|[a-z]{1,2}\)\s|[A-Z]\s{1,3}[A-ZÄÖÜ]|[IVX]+\.\s|[-–•▪◦]\s)")]
    private static partial Regex StructureMarkerPattern();

    [GeneratedRegex(@"^\p{L}+")]
    private static partial Regex FirstWordPattern();
}
