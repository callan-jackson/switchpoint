namespace SwitchPoint.Reports.Model;

/// <summary>
/// The format-independent content of a report. Composers build one of these from a <c>ReportRequest</c>;
/// the PDF and DOCX renderers walk the same model so every format carries the same sections in the same order.
/// </summary>
public sealed record ReportModel(
    string ReportId,
    string FirmName,
    string FirmReferenceNumber,
    string ClientName,
    string GeneratedBy,
    DateTime GeneratedAtUtc,
    string TemplateVersion,
    ReportCover Cover,
    IReadOnlyList<ReportSection> Sections);

/// <summary>Cover page: the report title, an optional subtitle and a list of facts (client, dates, identifiers).</summary>
public sealed record ReportCover(string Title, string Subtitle, IReadOnlyList<KeyValuePair<string, string>> Facts);

/// <summary>A titled group of blocks; the title renders as a level-1 heading.</summary>
public sealed record ReportSection(string Title, IReadOnlyList<ReportBlock> Blocks, bool StartOnNewPage = false);

/// <summary>Base of every content block.</summary>
public abstract record ReportBlock;

/// <summary>A heading below the section title (level 2 or 3).</summary>
public sealed record HeadingBlock(int Level, string Text) : ReportBlock;

public enum ParagraphStyle
{
    Normal = 0,
    Emphasis = 1,
    Small = 2,
    /// <summary>Prescribed regulatory wording, set apart from the surrounding text.</summary>
    Quote = 3,
    /// <summary>Text the adviser must replace before issue.</summary>
    Placeholder = 4,
}

/// <summary>A run of prose.</summary>
public sealed record ParagraphBlock(string Text, ParagraphStyle Style = ParagraphStyle.Normal) : ReportBlock;

/// <summary>A bulleted (or numbered) list.</summary>
public sealed record ListBlock(IReadOnlyList<string> Items, bool Numbered = false) : ReportBlock;

/// <summary>Two-column label/value facts.</summary>
public sealed record KeyValueBlock(IReadOnlyList<KeyValuePair<string, string>> Rows, string? Title = null) : ReportBlock;

public enum ColumnAlign
{
    Left = 0,
    Right = 1,
    Centre = 2,
}

/// <summary>A table column: header text, alignment and relative width.</summary>
public sealed record TableColumn(string Header, ColumnAlign Align = ColumnAlign.Left, double RelativeWidth = 1);

/// <summary>A data table with an optional bold footer row and a note underneath.</summary>
public sealed record TableBlock(IReadOnlyList<TableColumn> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, string? Title = null, IReadOnlyList<string>? FooterRow = null, string? Note = null) : ReportBlock;

/// <summary>A chart: an SVG drawing for the PDF plus the underlying numbers as a table for formats that cannot draw.</summary>
public sealed record ChartBlock(string Title, string Svg, TableBlock Data, string? Caption = null, double HeightMm = 88) : ReportBlock;

public enum CalloutKind
{
    Info = 0,
    Warning = 1,
    Regulatory = 2,
}

/// <summary>A boxed notice (warnings, regulatory statements, notes).</summary>
public sealed record CalloutBlock(CalloutKind Kind, string Title, IReadOnlyList<string> Paragraphs) : ReportBlock;

/// <summary>Forces the following content onto a new page.</summary>
public sealed record PageBreakBlock : ReportBlock;

/// <summary>Fluent helper used by the composers to assemble a section.</summary>
public sealed class SectionBuilder
{
    private readonly List<ReportBlock> _blocks = [];

    public SectionBuilder(string title, bool startOnNewPage = false)
    {
        Title = title;
        StartOnNewPage = startOnNewPage;
    }

    public string Title { get; }

    public bool StartOnNewPage { get; }

    public SectionBuilder H2(string text) => Add(new HeadingBlock(2, text));

    public SectionBuilder H3(string text) => Add(new HeadingBlock(3, text));

    public SectionBuilder P(string text, ParagraphStyle style = ParagraphStyle.Normal) => Add(new ParagraphBlock(text, style));

    public SectionBuilder Placeholder(string text) => Add(new ParagraphBlock(text, ParagraphStyle.Placeholder));

    public SectionBuilder Quote(string text) => Add(new ParagraphBlock(text, ParagraphStyle.Quote));

    public SectionBuilder Bullets(IEnumerable<string> items) => Add(new ListBlock([.. items]));

    public SectionBuilder Numbered(IEnumerable<string> items) => Add(new ListBlock([.. items], Numbered: true));

    public SectionBuilder Facts(IEnumerable<KeyValuePair<string, string>> rows, string? title = null) => Add(new KeyValueBlock([.. rows], title));

    public SectionBuilder Table(TableBlock table) => Add(table);

    public SectionBuilder Chart(ChartBlock chart) => Add(chart);

    public SectionBuilder Callout(CalloutKind kind, string title, IEnumerable<string> paragraphs) => Add(new CalloutBlock(kind, title, [.. paragraphs]));

    public SectionBuilder PageBreak() => Add(new PageBreakBlock());

    public SectionBuilder Add(ReportBlock block)
    {
        _blocks.Add(block);
        return this;
    }

    public ReportSection Build() => new(Title, [.. _blocks], StartOnNewPage);
}
