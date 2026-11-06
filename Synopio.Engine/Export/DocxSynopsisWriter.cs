using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Synopio.Engine.Comparison;

namespace Synopio.Engine.Export;

public static class DocxSynopsisWriter
{
    // Constants
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string DeletedColor = "C00000";
    private const string InsertedColor = "00703C";
    private const string HeaderShading = "D9E8DF";
    private const int PageWidth = 16838;
    private const int PageHeight = 11906;
    private const int PageMargin = 850;

    // Methods
    public static void Write(Stream stream, Synopsis synopsis, DocxExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(synopsis);
        ArgumentNullException.ThrowIfNull(options);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(archive, "[Content_Types].xml", ContentTypes);
        WriteEntry(archive, "_rels/.rels", PackageRelationships);
        WriteEntry(archive, "word/_rels/document.xml.rels", DocumentRelationships);
        WriteEntry(archive, "word/styles.xml", Styles);
        WriteEntry(archive, "word/settings.xml", Settings);
        WriteFooter(archive, options);
        WriteDocument(archive, synopsis, options);
    }

    private static void WriteDocument(ZipArchive archive, Synopsis synopsis, DocxExportOptions options)
    {
        var entry = archive.CreateEntry("word/document.xml", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = XmlWriter.Create(entryStream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });

        writer.WriteStartDocument(true);
        writer.WriteStartElement("w", "document", WordNamespace);
        writer.WriteStartElement("w", "body", WordNamespace);

        WriteParagraph(writer, [new Segment(options.Title, SegmentKind.Equal)], "Title");

        var columnWidth = (PageWidth - (2 * PageMargin)) / 2;
        writer.WriteStartElement("w", "tbl", WordNamespace);
        writer.WriteStartElement("w", "tblPr", WordNamespace);
        WriteValueElement(writer, "tblStyle", "TableGrid");
        writer.WriteStartElement("w", "tblW", WordNamespace);
        writer.WriteAttributeString("w", "w", WordNamespace, (columnWidth * 2).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("w", "type", WordNamespace, "dxa");
        writer.WriteEndElement();
        WriteValueElement(writer, "tblLayout", "fixed", "type");
        writer.WriteEndElement();

        writer.WriteStartElement("w", "tblGrid", WordNamespace);
        for (var column = 0; column < 2; column++)
        {
            writer.WriteStartElement("w", "gridCol", WordNamespace);
            writer.WriteAttributeString("w", "w", WordNamespace, columnWidth.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();

        WriteRow(writer, columnWidth, isHeader: true,
            [new Segment(options.LeftHeading, SegmentKind.Equal)],
            [new Segment(options.RightHeading, SegmentKind.Equal)], null, null, options);

        foreach (var row in synopsis.Rows.Where(row => !options.OnlyChangedRows || row.HasChanges))
        {
            WriteRow(writer, columnWidth, isHeader: false, row.Left, row.Right, row.LeftPage, row.RightPage, options);
        }

        writer.WriteEndElement();

        writer.WriteStartElement("w", "sectPr", WordNamespace);
        writer.WriteStartElement("w", "footerReference", WordNamespace);
        writer.WriteAttributeString("w", "type", WordNamespace, "default");
        writer.WriteAttributeString("r", "id", RelationshipNamespace, "rId3");
        writer.WriteEndElement();
        writer.WriteStartElement("w", "pgSz", WordNamespace);
        writer.WriteAttributeString("w", "w", WordNamespace, PageWidth.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("w", "h", WordNamespace, PageHeight.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("w", "orient", WordNamespace, "landscape");
        writer.WriteEndElement();
        writer.WriteStartElement("w", "pgMar", WordNamespace);
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            writer.WriteAttributeString("w", side, WordNamespace, PageMargin.ToString(CultureInfo.InvariantCulture));
        }

        writer.WriteAttributeString("w", "header", WordNamespace, "425");
        writer.WriteAttributeString("w", "footer", WordNamespace, "425");
        writer.WriteAttributeString("w", "gutter", WordNamespace, "0");
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteFooter(ZipArchive archive, DocxExportOptions options)
    {
        var entry = archive.CreateEntry("word/footer1.xml", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = XmlWriter.Create(entryStream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });

        writer.WriteStartDocument(true);
        writer.WriteStartElement("w", "ftr", WordNamespace);
        writer.WriteStartElement("w", "p", WordNamespace);
        writer.WriteStartElement("w", "pPr", WordNamespace);
        WriteValueElement(writer, "pStyle", "Footer");
        writer.WriteStartElement("w", "tabs", WordNamespace);
        writer.WriteStartElement("w", "tab", WordNamespace);
        writer.WriteAttributeString("w", "val", WordNamespace, "right");
        writer.WriteAttributeString("w", "pos", WordNamespace, (PageWidth - (2 * PageMargin)).ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        if (options.FooterText is { Length: > 0 } footerText)
        {
            WriteRun(writer, footerText);
        }

        if (options.ShowPageNumbers)
        {
            writer.WriteStartElement("w", "r", WordNamespace);
            writer.WriteStartElement("w", "tab", WordNamespace);
            writer.WriteEndElement();
            writer.WriteEndElement();
            WriteRun(writer, options.PageLabel + " ");
            WriteField(writer, "PAGE");
            WriteRun(writer, " / ");
            WriteField(writer, "NUMPAGES");
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRun(XmlWriter writer, string text)
    {
        writer.WriteStartElement("w", "r", WordNamespace);
        writer.WriteStartElement("w", "t", WordNamespace);
        writer.WriteAttributeString("xml", "space", null, "preserve");
        writer.WriteString(RemoveInvalidXmlCharacters(text));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteField(XmlWriter writer, string instruction)
    {
        writer.WriteStartElement("w", "fldSimple", WordNamespace);
        writer.WriteAttributeString("w", "instr", WordNamespace, instruction);
        WriteRun(writer, "1");
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int columnWidth, bool isHeader, IReadOnlyList<Segment> left, IReadOnlyList<Segment> right,
        int? leftPage, int? rightPage, DocxExportOptions options)
    {
        writer.WriteStartElement("w", "tr", WordNamespace);
        writer.WriteStartElement("w", "trPr", WordNamespace);
        if (isHeader)
        {
            writer.WriteStartElement("w", "tblHeader", WordNamespace);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();

        WriteCell(writer, columnWidth, isHeader, left, leftPage, options);
        WriteCell(writer, columnWidth, isHeader, right, rightPage, options);
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, int columnWidth, bool isHeader, IReadOnlyList<Segment> segments, int? page, DocxExportOptions options)
    {
        writer.WriteStartElement("w", "tc", WordNamespace);
        writer.WriteStartElement("w", "tcPr", WordNamespace);
        writer.WriteStartElement("w", "tcW", WordNamespace);
        writer.WriteAttributeString("w", "w", WordNamespace, columnWidth.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("w", "type", WordNamespace, "dxa");
        writer.WriteEndElement();
        if (isHeader)
        {
            writer.WriteStartElement("w", "shd", WordNamespace);
            writer.WriteAttributeString("w", "val", WordNamespace, "clear");
            writer.WriteAttributeString("w", "color", WordNamespace, "auto");
            writer.WriteAttributeString("w", "fill", WordNamespace, HeaderShading);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();

        if (!isHeader && options.ShowPageNumbers && page is { } pageNumber)
        {
            WriteParagraph(writer, [new Segment(string.Create(CultureInfo.InvariantCulture, $"{options.PageLabel} {pageNumber}"), SegmentKind.Equal)], "PageReference");
        }

        var paragraph = new List<Segment>();
        var wroteParagraph = false;
        foreach (var segment in segments)
        {
            if (segment.Kind == SegmentKind.ParagraphBreak)
            {
                WriteParagraph(writer, paragraph, isHeader ? "TableHeading" : null);
                paragraph.Clear();
                wroteParagraph = true;
                continue;
            }

            paragraph.Add(segment);
        }

        if (paragraph.Count > 0 || !wroteParagraph)
        {
            WriteParagraph(writer, paragraph, isHeader ? "TableHeading" : null);
        }

        writer.WriteEndElement();
    }

    private static void WriteParagraph(XmlWriter writer, IEnumerable<Segment> segments, string? style)
    {
        writer.WriteStartElement("w", "p", WordNamespace);
        if (style != null)
        {
            writer.WriteStartElement("w", "pPr", WordNamespace);
            WriteValueElement(writer, "pStyle", style);
            writer.WriteEndElement();
        }

        foreach (var segment in segments)
        {
            writer.WriteStartElement("w", "r", WordNamespace);
            if (segment.Kind is SegmentKind.Deleted or SegmentKind.Inserted)
            {
                writer.WriteStartElement("w", "rPr", WordNamespace);
                if (segment.Kind == SegmentKind.Deleted)
                {
                    writer.WriteStartElement("w", "strike", WordNamespace);
                    writer.WriteEndElement();
                    WriteValueElement(writer, "color", DeletedColor);
                }
                else
                {
                    writer.WriteStartElement("w", "b", WordNamespace);
                    writer.WriteEndElement();
                    WriteValueElement(writer, "color", InsertedColor);
                    WriteValueElement(writer, "u", "single");
                }

                writer.WriteEndElement();
            }

            writer.WriteStartElement("w", "t", WordNamespace);
            writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(RemoveInvalidXmlCharacters(segment.Text));
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteValueElement(XmlWriter writer, string name, string value, string attribute = "val")
    {
        writer.WriteStartElement("w", name, WordNamespace);
        writer.WriteAttributeString("w", attribute, WordNamespace, value);
        writer.WriteEndElement();
    }

    private static string RemoveInvalidXmlCharacters(string text) =>
        string.Concat(text.Where(character => XmlConvert.IsXmlChar(character) || char.IsSurrogate(character)));

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    // Package parts
    private const string ContentTypes =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
          <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
          <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
        </Types>
        """;

    private const string PackageRelationships =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelationships =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
        </Relationships>
        """;

    private const string Settings =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:defaultTabStop w:val="708"/>
          <w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat>
        </w:settings>
        """;

    private const string Styles =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:docDefaults>
            <w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="Calibri" w:cs="Calibri"/><w:sz w:val="20"/><w:szCs w:val="20"/><w:lang w:val="de-DE"/></w:rPr></w:rPrDefault>
            <w:pPrDefault><w:pPr><w:spacing w:after="80" w:line="259" w:lineRule="auto"/></w:pPr></w:pPrDefault>
          </w:docDefaults>
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>
          <w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="200"/></w:pPr><w:rPr><w:b/><w:color w:val="1B2B23"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="TableHeading"><w:name w:val="Table Heading"/><w:basedOn w:val="Normal"/><w:rPr><w:b/><w:color w:val="1B2B23"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Footer"><w:name w:val="footer"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0"/></w:pPr><w:rPr><w:color w:val="595959"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="PageReference"><w:name w:val="Page Reference"/><w:basedOn w:val="Normal"/><w:rPr><w:color w:val="595959"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr></w:style>
          <w:style w:type="table" w:default="1" w:styleId="TableNormal"><w:name w:val="Normal Table"/><w:tblPr><w:tblInd w:w="0" w:type="dxa"/><w:tblCellMar><w:top w:w="0" w:type="dxa"/><w:left w:w="108" w:type="dxa"/><w:bottom w:w="0" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>
          <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:basedOn w:val="TableNormal"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/><w:left w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/><w:right w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/><w:insideH w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/><w:insideV w:val="single" w:sz="4" w:space="0" w:color="86BC9C"/></w:tblBorders><w:tblCellMar><w:top w:w="57" w:type="dxa"/><w:bottom w:w="57" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>
        </w:styles>
        """;
}
