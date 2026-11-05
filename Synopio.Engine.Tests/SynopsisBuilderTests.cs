using Synopio.Engine.Comparison;
using Synopio.Engine.Text;

namespace Synopio.Engine.Tests;

public class SynopsisBuilderTests
{
    // Helpers
    private static List<Paragraph> Paragraphs(params string[] texts) => [.. texts.Select(text => new Paragraph(text, 1))];

    private static string Render(IEnumerable<Segment> segments) =>
        string.Concat(segments.Select(segment => segment.Kind switch
        {
            SegmentKind.Deleted => "[-" + segment.Text + "-]",
            SegmentKind.Inserted => "{+" + segment.Text + "+}",
            SegmentKind.ParagraphBreak => " | ",
            _ => segment.Text,
        }));

    // Tests
    [Fact]
    public void IdenticalTextsProduceOnlyUnchangedRows()
    {
        var synopsis = SynopsisBuilder.Compare(Paragraphs("Erster Absatz.", "Zweiter Absatz."), Paragraphs("Erster Absatz.", "Zweiter Absatz."), new ComparisonOptions());

        Assert.All(synopsis.Rows, row => Assert.Equal(RowKind.Unchanged, row.Kind));
        Assert.Equal(2, synopsis.Rows.Count);
    }

    [Fact]
    public void SingleWordChangeIsMarkedPrecisely()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Die Bücherei hat an vier Tagen geöffnet."),
            Paragraphs("Die Bücherei hat an vier weiteren Tagen geöffnet."),
            new ComparisonOptions());

        var row = Assert.Single(synopsis.Rows);
        Assert.Equal(RowKind.Modified, row.Kind);
        Assert.Equal("Die Bücherei hat an vier Tagen geöffnet.", Render(row.Left));
        Assert.Equal("Die Bücherei hat an vier {+weiteren+} Tagen geöffnet.", Render(row.Right));
        Assert.Equal(1, synopsis.Statistics.InsertedWords);
    }

    [Fact]
    public void ReplacedWordIsDeletedAndInserted()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Im Jahr 2026 wird eine Inventur durchgeführt."),
            Paragraphs("Im Jahr 2026 wurde eine Inventur durchgeführt."),
            new ComparisonOptions());

        var row = Assert.Single(synopsis.Rows);
        Assert.Equal("Im Jahr 2026 [-wird-] eine Inventur durchgeführt.", Render(row.Left));
        Assert.Equal("Im Jahr 2026 {+wurde+} eine Inventur durchgeführt.", Render(row.Right));
    }

    [Fact]
    public void SplitParagraphIsShownInOneUnchangedRow()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Satz eins steht hier. Satz zwei folgt danach."),
            Paragraphs("Satz eins steht hier.", "Satz zwei folgt danach."),
            new ComparisonOptions());

        var row = Assert.Single(synopsis.Rows);
        Assert.Equal(RowKind.Unchanged, row.Kind);
        Assert.Equal("Satz eins steht hier. | Satz zwei folgt danach.", Render(row.Right));
    }

    [Fact]
    public void NewParagraphBecomesInsertedRow()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("§ 1 Geltungsbereich", "§ 2 Begriffe"),
            Paragraphs("§ 1 Geltungsbereich", "§ 1a Digitale Medien werden kostenlos verliehen.", "§ 2 Begriffe"),
            new ComparisonOptions());

        Assert.Equal([RowKind.Unchanged, RowKind.Inserted, RowKind.Unchanged], synopsis.Rows.Select(row => row.Kind));
    }

    [Fact]
    public void RemovedParagraphBecomesDeletedRow()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Absatz A bleibt.", "Absatz B fällt komplett weg.", "Absatz C bleibt."),
            Paragraphs("Absatz A bleibt.", "Absatz C bleibt."),
            new ComparisonOptions());

        Assert.Equal([RowKind.Unchanged, RowKind.Deleted, RowKind.Unchanged], synopsis.Rows.Select(row => row.Kind));
    }

    [Fact]
    public void TypographicQuotesAndHyphenationAreIgnored()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Der \"Lesesommer\" der nordrhein-westfälischen Bücherei."),
            Paragraphs("Der „Lesesommer“ der nordrheinwestfälischen Bücherei."),
            new ComparisonOptions());

        Assert.Equal(RowKind.Unchanged, Assert.Single(synopsis.Rows).Kind);
    }

    [Fact]
    public void IgnoreCaseOptionIsRespected()
    {
        var left = Paragraphs("Die Benutzungsordnung gilt.");
        var right = Paragraphs("Die BENUTZUNGSORDNUNG gilt.");

        Assert.Equal(RowKind.Modified, Assert.Single(SynopsisBuilder.Compare(left, right, new ComparisonOptions()).Rows).Kind);
        Assert.Equal(RowKind.Unchanged, Assert.Single(SynopsisBuilder.Compare(left, right, new ComparisonOptions { IgnoreCase = true }).Rows).Kind);
    }

    [Fact]
    public void IgnorePunctuationOptionIsRespected()
    {
        var synopsis = SynopsisBuilder.Compare(Paragraphs("Erstens, zweitens."), Paragraphs("Erstens zweitens"), new ComparisonOptions { IgnorePunctuation = true });

        Assert.Equal(RowKind.Unchanged, Assert.Single(synopsis.Rows).Kind);
    }

    [Fact]
    public void RewrittenParagraphDoesNotKeepScatteredCommonWords()
    {
        var synopsis = SynopsisBuilder.Compare(
            Paragraphs("Die Ausleihfrist für Bücher beträgt vier Wochen und kann zweimal verlängert werden, sofern keine Vormerkung vorliegt."),
            Paragraphs("Im Lesesaal sind Getränke in verschließbaren Flaschen erlaubt, solange die Tische sauber bleiben."),
            new ComparisonOptions());

        var equalLength = synopsis.Rows.SelectMany(row => row.Right).Where(segment => segment.Kind == SegmentKind.Equal).Sum(segment => segment.Text.Trim().Length);
        Assert.True(equalLength <= 4, $"Unexpected matches: {equalLength} characters");
    }

    [Fact]
    public void EmptyInputsProduceNoRows()
    {
        var synopsis = SynopsisBuilder.Compare([], [], new ComparisonOptions());

        Assert.Empty(synopsis.Rows);
    }
}
