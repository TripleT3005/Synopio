using System.Text;
using System.Text.RegularExpressions;

namespace Synopio.Engine.Documents;

public sealed partial class MarkdownDocumentReader : IDocumentReader
{
    // Properties
    public IReadOnlyCollection<string> Extensions { get; } = [".md", ".markdown"];

    // Methods
    public SourceDocument Read(Stream stream, string fileName, string sha256)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return new SourceDocument
        {
            FileName = fileName,
            Sha256 = sha256,
            HasLayout = false,
            Lines = TextDocumentReader.Parse(RemoveBlockMarkers(reader.ReadToEnd())),
        };
    }

    internal static string RemoveBlockMarkers(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(line => HeadingPattern().IsMatch(line)
                ? HeadingClosePattern().Replace(BlockMarkerPattern().Replace(line, string.Empty), string.Empty)
                : BlockMarkerPattern().Replace(line, string.Empty));
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"^\s{0,3}(#{1,6}\s+|>\s?)")]
    private static partial Regex BlockMarkerPattern();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"\s+#+\s*$")]
    private static partial Regex HeadingClosePattern();
}
