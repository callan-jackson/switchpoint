using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SwitchPoint.Application.Ports;
using SwitchPoint.Reports.Composition;
using SwitchPoint.Reports.Model;

namespace SwitchPoint.Reports.Rendering;

/// <summary>
/// Renders a <see cref="ReportModel"/> as a Word document. The section order matches the PDF; charts are written as
/// their underlying data tables (with a note) so the document has no embedded images.
/// </summary>
public static class DocxReportRenderer
{
    public static ReportDocument Render(ReportRequest request, string templateVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReportModel model = ReportComposer.Compose(request, templateVersion);

        using MemoryStream stream = new();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            main.Document = new Document();
            Body body = main.Document.AppendChild(new Body());
            AddStyles(main);

            body.AppendChild(Heading(model.Cover.Title, 1));
            body.AppendChild(Para(model.Cover.Subtitle, italic: true));
            body.AppendChild(KeyValueTable(model.Cover.Facts));
            body.AppendChild(Para($"{model.FirmName} · FCA firm reference {model.FirmReferenceNumber}", small: true));

            foreach (ReportSection section in model.Sections)
            {
                body.AppendChild(Heading(section.Title, 1));
                foreach (ReportBlock block in section.Blocks)
                {
                    foreach (OpenXmlElement element in Block(block))
                    {
                        body.AppendChild(element);
                    }
                }
            }

            body.AppendChild(Para($"Report {model.ReportId} · generated {model.GeneratedAtUtc:d MMMM yyyy HH:mm} UTC by {model.GeneratedBy} · template {model.TemplateVersion}", small: true));
            main.Document.Save();
        }

        return new ReportDocument(stream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{request.Kind}-{request.AnalysisId:N}-v{request.AnalysisVersion}.docx", templateVersion);
    }

    private static IEnumerable<OpenXmlElement> Block(ReportBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                yield return Heading(h.Text, h.Level);
                break;
            case ParagraphBlock p:
                yield return Para(p.Text, italic: p.Style is ParagraphStyle.Quote or ParagraphStyle.Placeholder, bold: p.Style == ParagraphStyle.Emphasis, small: p.Style == ParagraphStyle.Small);
                break;
            case ListBlock l:
                int i = 1;
                foreach (string item in l.Items)
                {
                    yield return Para(l.Numbered ? $"{i}. {item}" : $"• {item}");
                    i++;
                }

                break;
            case KeyValueBlock kv:
                if (kv.Title is { } kvTitle)
                {
                    yield return Heading(kvTitle, 3);
                }

                yield return KeyValueTable(kv.Rows);
                yield return Para(string.Empty);
                break;
            case TableBlock t:
                if (t.Title is { } tableTitle)
                {
                    yield return Heading(tableTitle, 3);
                }

                yield return DataTable(t);
                if (t.Note is { } note)
                {
                    yield return Para(note, small: true);
                }

                yield return Para(string.Empty);
                break;
            case ChartBlock c:
                yield return Heading(c.Title, 3);
                yield return Para(c.Caption ?? "The chart is shown as its underlying data in this format.", small: true);
                yield return DataTable(c.Data);
                yield return Para(string.Empty);
                break;
            case CalloutBlock c:
                yield return Heading(c.Title, 3);
                foreach (string paragraph in c.Paragraphs)
                {
                    yield return Para(paragraph);
                }

                break;
            case PageBreakBlock:
                yield return new Paragraph(new Run(new Break { Type = BreakValues.Page }));
                break;
            default:
                yield break;
        }
    }

    private static Paragraph Heading(string text, int level)
    {
        int size = level switch { 1 => 32, 2 => 26, _ => 22 };
        Paragraph p = new(new Run(new RunProperties(new Bold(), new FontSize { Val = size.ToString(System.Globalization.CultureInfo.InvariantCulture) }, new Color { Val = "1F4E79" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        p.ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" });
        return p;
    }

    private static Paragraph Para(string text, bool bold = false, bool italic = false, bool small = false)
    {
        RunProperties properties = new();
        if (bold)
        {
            properties.AppendChild(new Bold());
        }

        if (italic)
        {
            properties.AppendChild(new Italic());
        }

        properties.AppendChild(new FontSize { Val = small ? "16" : "20" });
        if (small)
        {
            properties.AppendChild(new Color { Val = "6E6E6E" });
        }

        return new Paragraph(new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table KeyValueTable(IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        Table table = NewTable();
        foreach ((string key, string value) in rows)
        {
            table.AppendChild(new TableRow(Cell(key, bold: false, muted: true), Cell(value)));
        }

        return table;
    }

    private static Table DataTable(TableBlock block)
    {
        Table table = NewTable();
        table.AppendChild(new TableRow([.. block.Columns.Select(c => Cell(c.Header, bold: true))]));
        foreach (IReadOnlyList<string> row in block.Rows)
        {
            table.AppendChild(new TableRow([.. block.Columns.Select((_, i) => Cell(i < row.Count ? row[i] : string.Empty))]));
        }

        if (block.FooterRow is { } footer)
        {
            table.AppendChild(new TableRow([.. block.Columns.Select((_, i) => Cell(i < footer.Count ? footer[i] : string.Empty, bold: true))]));
        }

        return table;
    }

    private static Table NewTable()
    {
        Table table = new();
        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "DDDDDD" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "EEEEEE" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "EEEEEE" }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }));
        return table;
    }

    private static TableCell Cell(string text, bool bold = false, bool muted = false)
    {
        RunProperties properties = new();
        if (bold)
        {
            properties.AppendChild(new Bold());
        }

        properties.AppendChild(new FontSize { Val = "18" });
        if (muted)
        {
            properties.AppendChild(new Color { Val = "6E6E6E" });
        }

        return new TableCell(new Paragraph(new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static void AddStyles(MainDocumentPart main)
    {
        StyleDefinitionsPart part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = "20" }))));
        part.Styles.Save();
    }
}
