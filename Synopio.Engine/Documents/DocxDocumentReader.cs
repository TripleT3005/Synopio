using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Synopio.Engine.Documents;

public sealed class DocxDocumentReader : IDocumentReader
{
    // Constants
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    // Properties
    public IReadOnlyCollection<string> Extensions { get; } = [".docx"];

    // Methods
    public SourceDocument Read(Stream stream, string fileName, string sha256)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("word/document.xml");
        using var entryStream = entry.Open();

        return new SourceDocument
        {
            FileName = fileName,
            Sha256 = sha256,
            HasLayout = false,
            Lines = ParseDocument(entryStream),
        };
    }

    private static List<SourceLine> ParseDocument(Stream documentXml)
    {
        var lines = new List<SourceLine>();
        var text = new StringBuilder();
        var page = 1;
        var pendingPageBreaks = 0;
        var deletedDepth = 0;

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false };
        using var reader = XmlReader.Create(documentXml, settings);

        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NamespaceURI == WordNamespace && reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && deletedDepth == 0)
            {
                text.Append(reader.ReadElementContentAsString());
                continue;
            }

            HandleNode();
            reader.Read();
        }

        return lines;

        void HandleNode()
        {
            if (reader.NamespaceURI != WordNamespace)
            {
                return;
            }

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                switch (reader.LocalName)
                {
                    case "del":
                        deletedDepth--;
                        break;
                    case "p":
                        var paragraph = text.ToString().Trim();
                        if (paragraph.Length > 0)
                        {
                            lines.Add(new SourceLine(paragraph, page) { StartsParagraph = true });
                        }

                        text.Clear();
                        page += pendingPageBreaks;
                        pendingPageBreaks = 0;
                        break;
                }

                return;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                return;
            }

            switch (reader.LocalName)
            {
                case "del" when !reader.IsEmptyElement:
                    deletedDepth++;
                    break;
                case "tab":
                case "cr":
                    text.Append(' ');
                    break;
                case "br":
                    if (reader.GetAttribute("type", WordNamespace) == "page")
                    {
                        pendingPageBreaks++;
                    }
                    else
                    {
                        text.Append(' ');
                    }

                    break;
                case "lastRenderedPageBreak":
                    if (text.Length == 0)
                    {
                        page++;
                    }
                    else
                    {
                        pendingPageBreaks++;
                    }

                    break;
                case "noBreakHyphen":
                    text.Append('-');
                    break;
            }
        }
    }
}
