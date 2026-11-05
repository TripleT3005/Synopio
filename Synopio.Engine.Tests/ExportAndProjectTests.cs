using System.IO.Compression;
using System.Xml;
using Synopio.Engine.Comparison;
using Synopio.Engine.Documents;
using Synopio.Engine.Export;
using Synopio.Engine.Projects;
using Synopio.Engine.Text;

namespace Synopio.Engine.Tests;

public class ExportAndProjectTests
{
    // Helpers
    private static Synopsis CreateSynopsis() => SynopsisBuilder.Compare(
        [new Paragraph("§ 1 Die Frist beträgt drei Monate.", 1), new Paragraph("§ 2 Unverändert & gleich <bleibt>.", 2)],
        [new Paragraph("§ 1 Die Frist beträgt sechs Monate.", 1), new Paragraph("§ 2 Unverändert & gleich <bleibt>.", 2)],
        new ComparisonOptions());

    private static DocxExportOptions ExportOptions(bool onlyChanges = false) => new()
    {
        Title = "Synopse",
        LeftHeading = "alt.pdf",
        RightHeading = "neu.pdf",
        PageLabel = "Seite",
        OnlyChangedRows = onlyChanges,
        FooterText = "Erstellt mit Synopio 0.1.0",
    };

    private static MemoryStream Export(Synopsis synopsis, DocxExportOptions options)
    {
        var stream = new MemoryStream();
        DocxSynopsisWriter.Write(stream, synopsis, options);
        stream.Position = 0;
        return stream;
    }

    // Tests
    [Fact]
    public void DocxExportContainsValidDocumentWithMarkup()
    {
        using var stream = Export(CreateSynopsis(), ExportOptions());
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("word/styles.xml"));

        using (var footer = new StreamReader(archive.GetEntry("word/footer1.xml")!.Open()))
        {
            var footerXml = footer.ReadToEnd();
            Assert.Contains("Erstellt mit Synopio 0.1.0", footerXml, StringComparison.Ordinal);
            Assert.Contains("NUMPAGES", footerXml, StringComparison.Ordinal);
        }

        var document = new XmlDocument();
        using (var entry = archive.GetEntry("word/document.xml")!.Open())
        {
            document.Load(entry);
        }

        var namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

        Assert.Equal(3, document.SelectNodes("//w:tr", namespaces)!.Count);
        Assert.Equal("drei", document.SelectSingleNode("//w:r[w:rPr/w:strike]/w:t", namespaces)?.InnerText);
        Assert.Equal("sechs", document.SelectSingleNode("//w:r[w:rPr/w:u]/w:t", namespaces)?.InnerText);
    }

    [Fact]
    public void DocxExportCanBeReadBack()
    {
        using var stream = Export(CreateSynopsis(), ExportOptions());

        var document = new DocxDocumentReader().Read(stream, "synopse.docx", string.Empty);
        var text = string.Join("\n", document.Lines.Select(line => line.Text));

        Assert.Contains("§ 2 Unverändert & gleich <bleibt>.", text, StringComparison.Ordinal);
        Assert.Contains("Seite 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DocxExportCanSkipUnchangedRows()
    {
        using var stream = Export(CreateSynopsis(), ExportOptions(onlyChanges: true));

        var text = string.Join("\n", new DocxDocumentReader().Read(stream, "synopse.docx", string.Empty).Lines.Select(line => line.Text));

        Assert.DoesNotContain("§ 2", text, StringComparison.Ordinal);
        Assert.Contains("sechs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectStoresPathsRelativeToProjectFile()
    {
        var directory = Directory.CreateTempSubdirectory("synopio-test-");
        try
        {
            var documentPath = Path.Combine(directory.FullName, "Dokumente", "alt.pdf");
            var projectPath = Path.Combine(directory.FullName, "Vergleich.synopio");
            var project = new SynopioProject
            {
                Left = new ProjectDocument { Path = documentPath, Sha256 = "abc", FirstPage = 3, EditedText = "Korrigiert" },
                Comparison = new ComparisonOptions { IgnoreCase = true },
            };

            ProjectSerializer.Save(projectPath, project);
            var json = File.ReadAllText(projectPath);
            var loaded = ProjectSerializer.Load(projectPath);

            Assert.DoesNotContain(directory.FullName.Replace("\\", "\\\\", StringComparison.Ordinal), json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(documentPath, loaded.Left?.Path);
            Assert.Equal(3, loaded.Left?.FirstPage);
            Assert.Equal("Korrigiert", loaded.Left?.EditedText);
            Assert.True(loaded.Comparison.IgnoreCase);
            Assert.Null(loaded.Right);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
