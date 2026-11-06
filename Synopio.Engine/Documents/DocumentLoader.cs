using System.Security.Cryptography;

namespace Synopio.Engine.Documents;

public static class DocumentLoader
{
    // Fields
    private static readonly IDocumentReader[] Readers =
    [
        new PdfDocumentReader(),
        new DocxDocumentReader(),
        new TextDocumentReader(),
        new MarkdownDocumentReader(),
    ];

    // Properties
    public static IEnumerable<string> SupportedExtensions => Readers.SelectMany(reader => reader.Extensions);

    // Methods
    public static SourceDocument Load(string path)
    {
        var extension = Path.GetExtension(path);
        var reader = Readers.FirstOrDefault(candidate => candidate.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            ?? throw new NotSupportedException(extension);

        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        return reader.Read(stream, Path.GetFileName(path), ComputeSha256(bytes));
    }

    public static string ComputeSha256(string path) => ComputeSha256(File.ReadAllBytes(path));

    private static string ComputeSha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
