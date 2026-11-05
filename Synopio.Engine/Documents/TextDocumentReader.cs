using System.Text;

namespace Synopio.Engine.Documents;

public sealed class TextDocumentReader : IDocumentReader
{
    // Properties
    public IReadOnlyCollection<string> Extensions { get; } = [".txt"];

    // Methods
    public SourceDocument Read(Stream stream, string fileName, string sha256)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return new SourceDocument
        {
            FileName = fileName,
            Sha256 = sha256,
            HasLayout = false,
            Lines = Parse(reader.ReadToEnd()),
        };
    }

    internal static List<SourceLine> Parse(string text)
    {
        var lines = new List<SourceLine>();
        var page = 1;
        var startsParagraph = true;

        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var parts = rawLine.Split('\f');
            for (var part = 0; part < parts.Length; part++)
            {
                if (part > 0)
                {
                    page++;
                    startsParagraph = true;
                }

                var line = parts[part].TrimEnd();
                if (line.Trim().Length == 0)
                {
                    startsParagraph = true;
                    continue;
                }

                lines.Add(new SourceLine(line.Trim(), page) { StartsParagraph = startsParagraph, Left = line.Length - line.TrimStart().Length, Right = line.Length });
                startsParagraph = false;
            }
        }

        return lines;
    }
}
