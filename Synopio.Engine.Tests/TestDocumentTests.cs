using Synopio.Engine.Comparison;
using Synopio.Engine.Documents;
using Synopio.Engine.Text;

namespace Synopio.Engine.Tests;

public class TestDocumentTests
{
    // Fields
    private static readonly string[] Versions = ["satzung-2025", "satzung-2026-entwurf"];
    private static readonly string[] Formats = [".md", ".txt", ".docx", ".pdf", ".word.pdf"];

    // Helpers
    private static string PathOf(string version, string format) => Path.Combine(TestPaths.TestDataDirectory, version + format);

    private static Synopsis Compare(string leftPath, string rightPath)
    {
        var left = DocumentLoader.Load(leftPath);
        var right = DocumentLoader.Load(rightPath);
        return SynopsisBuilder.Compare(
            TextPreparer.Prepare(left, new PreparationOptions(), right),
            TextPreparer.Prepare(right, new PreparationOptions(), left),
            new ComparisonOptions());
    }

    private static List<string> ChangedText(Synopsis synopsis) =>
        [.. synopsis.Rows
            .SelectMany(row => row.Left.Concat(row.Right))
            .Where(segment => segment.Kind is SegmentKind.Deleted or SegmentKind.Inserted)
            .Select(segment => $"{segment.Kind}: {segment.Text.Trim()}")];

    public static TheoryData<string, string> SameVersionInOtherFormats()
    {
        var data = new TheoryData<string, string>();
        foreach (var version in Versions)
        {
            foreach (var format in Formats.Skip(1))
            {
                data.Add(version, format);
            }
        }

        return data;
    }

    public static TheoryData<string> AllFormats()
    {
        var data = new TheoryData<string>();
        foreach (var format in Formats)
        {
            data.Add(format);
        }

        return data;
    }

    // Tests
    [Theory]
    [MemberData(nameof(SameVersionInOtherFormats))]
    public void SameVersionInDifferentFormatsHasNoChanges(string version, string format)
    {
        var synopsis = Compare(PathOf(version, ".md"), PathOf(version, format));

        Assert.Empty(ChangedText(synopsis));
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void OldAndNewVersionShowTheIntendedChanges(string format)
    {
        var synopsis = Compare(PathOf(Versions[0], format), PathOf(Versions[1], format));
        var changes = ChangedText(synopsis);

        Assert.Contains("Deleted: zweimal", changes);
        Assert.Contains("Inserted: dreimal", changes);
        Assert.Contains("Deleted: 12", changes);
        Assert.Contains("Inserted: 15", changes);
        Assert.Contains(synopsis.Rows, row => row.Kind == RowKind.Inserted && row.Right.Any(segment => segment.Text.Contains("§ 4a Digitale Ausleihe", StringComparison.Ordinal)));
        Assert.Contains(synopsis.Rows, row => row.Kind == RowKind.Deleted && row.Left.Any(segment => segment.Text.Contains("Internetarbeitsplätze", StringComparison.Ordinal)));
        Assert.DoesNotContain(changes, change => change.Contains("Stadtbücherei Musterstadt", StringComparison.Ordinal) && change.Contains("Seite", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void AllFormatsFindTheSameChanges(string format)
    {
        var expected = ChangedText(Compare(PathOf(Versions[0], ".md"), PathOf(Versions[1], ".md")));
        var actual = ChangedText(Compare(PathOf(Versions[0], format), PathOf(Versions[1], format)));

        Assert.Equal(expected, actual);
    }
}
