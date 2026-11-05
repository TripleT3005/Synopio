namespace Synopio.Engine.Documents;

public interface IDocumentReader
{
    // Properties
    IReadOnlyCollection<string> Extensions { get; }

    // Methods
    SourceDocument Read(Stream stream, string fileName, string sha256);
}
