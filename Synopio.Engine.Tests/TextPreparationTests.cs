using Synopio.Engine.Documents;
using Synopio.Engine.Text;

namespace Synopio.Engine.Tests;

public class TextPreparationTests
{
    // Helpers
    private static SourceDocument LayoutDocument(params (string Text, int Page, double Top)[] lines) => new()
    {
        FileName = "test.pdf",
        Sha256 = string.Empty,
        HasLayout = true,
        Lines = [.. lines.Select(line => new SourceLine(line.Text, line.Page) { Top = line.Top, Bottom = line.Top + 10, Left = 50, Right = 500, FontSize = 11 })],
    };

    private static string Prepare(SourceDocument document, PreparationOptions? options = null) =>
        string.Join(" | ", TextPreparer.Prepare(document, options ?? new PreparationOptions()).Select(paragraph => paragraph.Text));

    // Tests
    [Fact]
    public void HyphenatedWordsAreJoined()
    {
        var document = LayoutDocument(("Die Benutzungsordnung gilt für alle Ver-", 1, 100), ("anstaltungen der Bücherei.", 1, 114));

        Assert.Equal("Die Benutzungsordnung gilt für alle Veranstaltungen der Bücherei.", Prepare(document));
    }

    [Fact]
    public void SuspendedHyphenBeforeConjunctionIsKept()
    {
        var document = LayoutDocument(("Die Regeln für Kinder-", 1, 100), ("und Jugendbücher gelten weiter.", 1, 114));

        Assert.Equal("Die Regeln für Kinder- und Jugendbücher gelten weiter.", Prepare(document));
    }

    [Fact]
    public void CompoundWithCapitalAfterHyphenKeepsHyphen()
    {
        var document = LayoutDocument(("Das Land Nordrhein-", 1, 100), ("Westfalen beschließt.", 1, 114));

        Assert.Equal("Das Land Nordrhein-Westfalen beschließt.", Prepare(document));
    }

    [Fact]
    public void LargeGapStartsNewParagraph()
    {
        var document = LayoutDocument(("Erster Absatz endet hier", 1, 100), ("weiter im ersten Absatz", 1, 114), ("Zweiter Absatz beginnt", 1, 150));

        Assert.Equal("Erster Absatz endet hier weiter im ersten Absatz | Zweiter Absatz beginnt", Prepare(document));
    }

    [Fact]
    public void StructureMarkersStartNewParagraph()
    {
        var document = LayoutDocument(("§ 3 Ausleihe", 1, 100), ("(1) Medien werden gegen Vorlage", 1, 114), ("(2) Zeitschriften dürfen nur", 1, 128));

        Assert.Equal("§ 3 Ausleihe | (1) Medien werden gegen Vorlage | (2) Zeitschriften dürfen nur", Prepare(document));
    }

    [Fact]
    public void RepeatedHeadersAndPageNumbersAreRemoved()
    {
        var lines = new List<(string, int, double)>();
        for (var page = 1; page <= 4; page++)
        {
            lines.Add(($"STADT MUSTERSTADT – Satzungsentwurf Nr. {100 + page}", page, 20));
            for (var line = 0; line < 6; line++)
            {
                lines.Add(($"Inhalt {line} der Seite {page} mit genug Text", page, 100 + (line * 14)));
            }

            lines.Add(($"Seite | {page}", page, 800));
        }

        var text = Prepare(LayoutDocument([.. lines]));

        Assert.DoesNotContain("MUSTERSTADT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Seite |", text, StringComparison.Ordinal);
        Assert.Contains("Inhalt 0 der Seite 3", text, StringComparison.Ordinal);
        Assert.Contains("Inhalt 5 der Seite 3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PageRangeIsApplied()
    {
        var document = LayoutDocument(("Seite eins.", 1, 100), ("Seite zwei.", 2, 100), ("Seite drei.", 3, 100));

        Assert.Equal("Seite zwei.", Prepare(document, new PreparationOptions { FirstPage = 2, LastPage = 2, RemoveHeadersAndFooters = false }));
    }

    [Fact]
    public void BrokenLigaturesAreRepairedFromVocabulary()
    {
        var document = LayoutDocument(
            ("Die Situation des Auftrags ist bekannt.", 1, 100),
            ("Die Situa=on des Au>rags ist gut.", 1, 114),
            ("Eine weitere Situa=on des Au>rags.", 1, 128),
            ("Noch eine Situa=on wegen des Au>rags.", 1, 142));

        var text = Prepare(document);

        Assert.DoesNotContain("=", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">", text, StringComparison.Ordinal);
        Assert.Contains("Auftrags.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LigaturesAreRepairedFromReferenceDocument()
    {
        var document = LayoutDocument(("Die MiAel wurden bewilligt.", 1, 100));
        var reference = LayoutDocument(("Haushalt und Mittel", 1, 100));

        Assert.Equal("Die Mittel wurden bewilligt.", string.Join(" ", TextPreparer.Prepare(document, new PreparationOptions(), reference).Select(paragraph => paragraph.Text)));
    }

    [Fact]
    public void LegitimateAbbreviationsAreNotChanged()
    {
        var document = LayoutDocument(("Die MwSt beträgt 19 %, siehe BGB und eBook.", 1, 100));

        Assert.Equal("Die MwSt beträgt 19 %, siehe BGB und eBook.", Prepare(document));
    }

    [Fact]
    public void LigatureCharactersAreExpanded()
    {
        var document = LayoutDocument(("Die Waldﬂäche an der Raiﬀeisenstraße.", 1, 100));

        Assert.Equal("Die Waldfläche an der Raiffeisenstraße.", Prepare(document));
    }

    [Fact]
    public void PreparedTextRoundTripKeepsParagraphsAndPages()
    {
        List<Paragraph> paragraphs = [new("Erster Absatz.", 1), new("Zweiter Absatz.", 1), new("Dritter Absatz.", 4)];

        Assert.Equal(paragraphs, PreparedText.Parse(PreparedText.Format(paragraphs)));
    }

    [Fact]
    public void PlainTextUsesBlankLinesAsParagraphBreaks()
    {
        var lines = TextDocumentReader.Parse("Erste Zeile\nweiter im Absatz\n\nZweiter Absatz\f\nDritte Seite");
        var document = new SourceDocument { FileName = "a.txt", Sha256 = string.Empty, HasLayout = false, Lines = lines };

        var paragraphs = TextPreparer.Prepare(document, new PreparationOptions());

        Assert.Equal(["Erste Zeile weiter im Absatz", "Zweiter Absatz", "Dritte Seite"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal(2, paragraphs[^1].PageNumber);
    }
}
