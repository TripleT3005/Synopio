using Synopio.Engine.Documents;

namespace Synopio.Engine.Text;

public static class TextPreparer
{
    // Methods
    public static IReadOnlyList<Paragraph> Prepare(SourceDocument document, PreparationOptions options, SourceDocument? reference = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var lines = SelectPages(document.Lines, options).Select(line => line with { Text = CharacterNormalizer.Normalize(line.Text) }).ToList();

        if (options.RemoveHeadersAndFooters && document.HasLayout)
        {
            lines = HeaderFooterFilter.Apply(lines);
        }

        if (options.RepairLigatures && document.HasLayout)
        {
            var repaired = LigatureRepair.Apply([.. lines.Select(line => line.Text)], reference?.Lines.Select(line => line.Text));
            lines = [.. lines.Select((line, index) => line with { Text = repaired[index] })];
        }

        return [.. ParagraphBuilder.Build(lines, document.HasLayout, options.JoinHyphenatedWords)
            .Select(paragraph => paragraph with { Text = CharacterNormalizer.RemoveSoftHyphens(paragraph.Text) })];
    }

    private static IEnumerable<SourceLine> SelectPages(IEnumerable<SourceLine> lines, PreparationOptions options) =>
        lines.Where(line =>
            (options.FirstPage is not { } first || line.PageNumber >= first) &&
            (options.LastPage is not { } last || line.PageNumber <= last));
}
