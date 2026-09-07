using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SwitchPoint.Application.Ports;
using SwitchPoint.Reports.Composition;
using SwitchPoint.Reports.Model;

namespace SwitchPoint.Reports.Rendering;

/// <summary>Renders a <see cref="ReportModel"/> as an A4 PDF with QuestPDF.</summary>
public static class PdfReportRenderer
{
    private const string Ink = "#333333";
    private const string Muted = "#6E6E6E";
    private const string Rule = "#DDDDDD";
    private const string Brand = "#1F4E79";
    private const string WarningTint = "#FDF3E7";
    private const string WarningInk = "#8A4B08";
    private const string NoteTint = "#EEF3F8";

    static PdfReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;
    }

    public static ReportDocument Render(ReportRequest request, string templateVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReportModel model = ReportComposer.Compose(request, templateVersion);
        byte[] bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink).LineHeight(1.25f).BreakAnywhere());
                page.Header().Element(h => Header(h, model));
                page.Footer().Element(f => Footer(f, model));
                page.Content().Element(c => Content(c, model));
            });
        }).GeneratePdf();

        return new ReportDocument(bytes, "application/pdf", $"{request.Kind}-{request.AnalysisId:N}-v{request.AnalysisVersion}.pdf", templateVersion);
    }

    private static void Header(IContainer container, ReportModel model) =>
        container.PaddingBottom(6).BorderBottom(0.75f).BorderColor(Rule).Row(row =>
        {
            row.RelativeItem().Text(model.FirmName).FontSize(8.5f).FontColor(Muted);
            row.ConstantItem(120).AlignRight().Text("Confidential").FontSize(8.5f).FontColor(Muted);
        });

    private static void Footer(IContainer container, ReportModel model) =>
        container.PaddingTop(6).BorderTop(0.75f).BorderColor(Rule).Row(row =>
        {
            row.RelativeItem().Text($"Report {model.ReportId} · generated {model.GeneratedAtUtc:d MMM yyyy HH:mm} UTC · template {model.TemplateVersion}").FontSize(7.5f).FontColor(Muted);
            row.ConstantItem(90).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });

    private static void Content(IContainer container, ReportModel model) =>
        container.Column(column =>
        {
            column.Spacing(10);
            Cover(column, model);
            foreach (ReportSection section in model.Sections)
            {
                if (section.StartOnNewPage)
                {
                    column.Item().PageBreak();
                }

                column.Item().PaddingTop(4).Text(section.Title).FontSize(14).SemiBold().FontColor(Brand);
                foreach (ReportBlock block in section.Blocks)
                {
                    column.Item().Element(c => Block(c, block));
                }
            }
        });

    private static void Cover(ColumnDescriptor column, ReportModel model)
    {
        column.Item().PaddingBottom(4).Text(model.Cover.Title).FontSize(24).Bold().FontColor(Brand);
        column.Item().Text(model.Cover.Subtitle).FontSize(13).FontColor(Muted);
        column.Item().PaddingTop(10).Element(c => KeyValues(c, new KeyValueBlock(model.Cover.Facts)));
        column.Item().PaddingTop(6).Text($"{model.FirmName} · Financial Conduct Authority firm reference {model.FirmReferenceNumber}").FontSize(8.5f).FontColor(Muted);
        column.Item().PageBreak();
    }

    private static void Block(IContainer container, ReportBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                container.PaddingTop(6).Text(h.Text).FontSize(h.Level == 2 ? 11.5f : 10.5f).SemiBold().FontColor(Ink);
                break;
            case ParagraphBlock p:
                Paragraph(container, p);
                break;
            case ListBlock l:
                container.Column(col =>
                {
                    int i = 1;
                    foreach (string item in l.Items)
                    {
                        col.Item().PaddingBottom(2).Row(row =>
                        {
                            row.ConstantItem(16).Text(l.Numbered ? $"{i}." : "•").FontColor(Muted);
                            row.RelativeItem().Text(item);
                        });
                        i++;
                    }
                });
                break;
            case KeyValueBlock kv:
                KeyValues(container, kv);
                break;
            case TableBlock t:
                Table(container, t);
                break;
            case ChartBlock c:
                Chart(container, c);
                break;
            case CalloutBlock c:
                Callout(container, c);
                break;
            case PageBreakBlock:
                container.PageBreak();
                break;
            default:
                container.Text(string.Empty);
                break;
        }
    }

    private static void Paragraph(IContainer container, ParagraphBlock p)
    {
        switch (p.Style)
        {
            case ParagraphStyle.Quote:
                container.PaddingVertical(4).Background(NoteTint).Padding(8).BorderLeft(3).BorderColor(Brand).Text(p.Text).FontSize(10).Italic();
                break;
            case ParagraphStyle.Placeholder:
                container.PaddingVertical(2).Text(p.Text).FontColor(Muted).Italic();
                break;
            case ParagraphStyle.Emphasis:
                container.PaddingVertical(2).Text(p.Text).SemiBold();
                break;
            case ParagraphStyle.Small:
                container.PaddingVertical(1).Text(p.Text).FontSize(8.5f).FontColor(Muted);
                break;
            default:
                container.PaddingVertical(2).Text(p.Text);
                break;
        }
    }

    private static void KeyValues(IContainer container, KeyValueBlock kv) =>
        container.Column(col =>
        {
            if (kv.Title is { } title)
            {
                col.Item().PaddingBottom(3).Text(title).SemiBold();
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.4f);
                });
                foreach ((string key, string value) in kv.Rows)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).Text(key).FontColor(Muted);
                    table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).Text(value);
                }
            });
        });

    private static void Table(IContainer container, TableBlock t) =>
        container.Column(col =>
        {
            if (t.Title is { } title)
            {
                col.Item().PaddingBottom(3).Text(title).SemiBold();
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    foreach (TableColumn column in t.Columns)
                    {
                        c.RelativeColumn((float)column.RelativeWidth);
                    }
                });

                table.Header(header =>
                {
                    foreach (TableColumn column in t.Columns)
                    {
                        IContainer cell = header.Cell().BorderBottom(1).BorderColor(Brand).PaddingVertical(3).PaddingHorizontal(2);
                        Align(cell, column.Align).Text(column.Header).FontSize(8.5f).SemiBold();
                    }
                });

                foreach (IReadOnlyList<string> row in t.Rows)
                {
                    for (int i = 0; i < t.Columns.Count; i++)
                    {
                        IContainer cell = table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(2.5f).PaddingHorizontal(2);
                        Align(cell, t.Columns[i].Align).Text(i < row.Count ? row[i] : string.Empty).FontSize(8.5f);
                    }
                }

                if (t.FooterRow is { } footer)
                {
                    for (int i = 0; i < t.Columns.Count; i++)
                    {
                        IContainer cell = table.Cell().BorderTop(1).BorderColor(Brand).PaddingVertical(3).PaddingHorizontal(2);
                        Align(cell, t.Columns[i].Align).Text(i < footer.Count ? footer[i] : string.Empty).FontSize(8.5f).SemiBold();
                    }
                }
            });

            if (t.Note is { } note)
            {
                col.Item().PaddingTop(3).Text(note).FontSize(8).FontColor(Muted);
            }
        });

    private static void Chart(IContainer container, ChartBlock chart) =>
        container.Column(col =>
        {
            col.Item().PaddingBottom(3).Text(chart.Title).SemiBold();
            // The SVG keeps its own aspect ratio; constraining only the width lets QuestPDF scale it without a layout conflict.
            col.Item().Svg(chart.Svg).FitWidth();
            if (chart.Caption is { } caption)
            {
                col.Item().PaddingTop(2).Text(caption).FontSize(8).FontColor(Muted);
            }

            col.Item().PaddingTop(6).Element(c => Table(c, chart.Data with { Title = chart.Data.Title ?? "The data behind the chart" }));
        });

    private static void Callout(IContainer container, CalloutBlock callout)
    {
        (string tint, string ink) = callout.Kind switch
        {
            CalloutKind.Warning => (WarningTint, WarningInk),
            _ => (NoteTint, Brand),
        };
        container.PaddingVertical(4).Background(tint).Padding(8).Column(col =>
        {
            col.Item().Text(callout.Title).SemiBold().FontColor(ink);
            foreach (string paragraph in callout.Paragraphs)
            {
                col.Item().PaddingTop(2).Text(paragraph).FontSize(9);
            }
        });
    }

    private static IContainer Align(IContainer container, ColumnAlign align) => align switch
    {
        ColumnAlign.Right => container.AlignRight(),
        ColumnAlign.Centre => container.AlignCenter(),
        _ => container,
    };
}
