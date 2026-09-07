using System.Globalization;
using System.Text;

namespace SwitchPoint.Reports.Charts;

/// <summary>A single (x, y) observation; x is a year or an age, y is a money amount.</summary>
public readonly record struct ChartPoint(decimal X, decimal Y);

/// <summary>One line on a <see cref="LineChart"/>. Dashed lines are used for the existing arrangement, solid for the proposed one.</summary>
public sealed record LineSeries(string Name, IReadOnlyList<ChartPoint> Points, string Colour, bool Dashed = false);

/// <summary>A line chart with money on the y-axis.</summary>
public sealed record LineChart(string Title, string XAxisTitle, string YAxisTitle, IReadOnlyList<LineSeries> Series, int Width = 720, int Height = 340);

/// <summary>One bar on a <see cref="BarChart"/>.</summary>
public sealed record BarItem(string Label, decimal Value, string Colour);

/// <summary>A vertical bar chart whose y-axis always starts at £0 (COBS 19 Annex 5 TVC layout).</summary>
public sealed record BarChart(string Title, string YAxisTitle, IReadOnlyList<BarItem> Bars, int Width = 560, int Height = 340);

/// <summary>One band of a <see cref="StackedAreaChart"/>; <see cref="Values"/> aligns with the chart's x values.</summary>
public sealed record AreaSeries(string Name, IReadOnlyList<decimal> Values, string Colour);

/// <summary>A stacked area chart; positive values stack above zero and negative values below.</summary>
public sealed record StackedAreaChart(string Title, string XAxisTitle, string YAxisTitle, IReadOnlyList<decimal> XValues, IReadOnlyList<AreaSeries> Series, int Width = 720, int Height = 340);

/// <summary>Percentile bands at one x value for a <see cref="FanChart"/>.</summary>
public readonly record struct FanPoint(decimal X, decimal P10, decimal P25, decimal P50, decimal P75, decimal P90);

/// <summary>A Monte Carlo fan chart: P10–P90 and P25–P75 shaded, median drawn as a line.</summary>
public sealed record FanChart(string Title, string XAxisTitle, string YAxisTitle, IReadOnlyList<FanPoint> Points, string Colour = ChartPalette.Proposed, int Width = 720, int Height = 340);

/// <summary>Colours shared by the report charts (print-safe, distinguishable in greyscale).</summary>
public static class ChartPalette
{
    public const string Existing = "#6E6E6E";
    public const string Proposed = "#1F4E79";
    public const string Accent = "#C55A11";
    public const string Positive = "#2E7D32";
    public const string Negative = "#B71C1C";
    public const string Neutral = "#9E9E9E";

    /// <summary>Series colours for multi-series charts, in order of use.</summary>
    public static IReadOnlyList<string> Series { get; } = ["#1F4E79", "#C55A11", "#2E7D32", "#6A1B9A", "#00838F", "#9E9D24", "#8D6E63", "#546E7A"];
}

/// <summary>
/// Draws the report charts as self-contained SVG documents (no external fonts, scripts or styles) so the same picture
/// can be embedded in a PDF by QuestPDF or served to a browser. Values are converted to <see cref="double"/> only
/// for pixel placement; the money on the labels is formatted from the original decimals.
/// </summary>
public static class SvgCharts
{
    private const string FontFamily = "Lato, Arial, Helvetica, sans-serif";
    private const string Ink = "#333333";
    private const string Grid = "#DDDDDD";
    private const string Axis = "#888888";

    /// <summary>Line chart: dashed vs solid series, £ axis labels, legend.</summary>
    public static string Line(LineChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        List<ChartPoint> all = [.. chart.Series.SelectMany(s => s.Points)];
        Frame f = Frame.For(chart.Width, chart.Height, legendRows: LegendRows(chart.Series.Count));
        (Scale xs, Scale ys) = Scales(f, all.Select(p => (double)p.X), all.Select(p => (double)p.Y), includeZero: true);

        SvgWriter w = new(chart.Width, chart.Height);
        w.Title(chart.Title, f);
        w.Grid(f, xs, ys, chart.XAxisTitle, chart.YAxisTitle);
        foreach (LineSeries s in chart.Series)
        {
            string dash = s.Dashed ? " stroke-dasharray=\"7 4\"" : string.Empty;
            IReadOnlyList<ChartPoint> pts = s.Points;
            if (pts.Count == 1)
            {
                w.Circle(xs.Map((double)pts[0].X), ys.Map((double)pts[0].Y), 4, s.Colour);
            }
            else if (pts.Count > 1)
            {
                w.Polyline(pts.Select(p => (xs.Map((double)p.X), ys.Map((double)p.Y))), s.Colour, 2.2, dash);
            }
        }

        w.Legend(f, chart.Series.Select(s => new LegendItem(s.Name, s.Colour, s.Dashed ? LegendStyle.DashedLine : LegendStyle.Line)));
        return w.Finish();
    }

    /// <summary>Vertical bars with the y-axis from £0 and the value printed above each bar.</summary>
    public static string Bars(BarChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        Frame f = Frame.For(chart.Width, chart.Height, legendRows: 0, bottomExtra: 22);
        Scale ys = Scale.Nice(chart.Bars.Select(b => (double)b.Value), includeZero: true, f.PlotBottom, f.PlotTop);
        SvgWriter w = new(chart.Width, chart.Height);
        w.Title(chart.Title, f);
        w.YAxis(f, ys, chart.YAxisTitle);
        int n = Math.Max(1, chart.Bars.Count);
        double slot = f.PlotWidth / n;
        double barWidth = Math.Min(slot * 0.55, 140);
        double zero = ys.Map(0);
        for (int i = 0; i < chart.Bars.Count; i++)
        {
            BarItem b = chart.Bars[i];
            double cx = f.PlotLeft + (slot * (i + 0.5));
            double y = ys.Map((double)b.Value);
            double top = Math.Min(y, zero);
            double h = Math.Abs(zero - y);
            w.Rect(cx - (barWidth / 2), top, barWidth, Math.Max(h, 0.5), b.Colour);
            w.Text(cx, top - 6, MoneyFull(b.Value), 12, "middle", Ink, bold: true);
            w.WrappedLabel(cx, f.PlotBottom + 16, b.Label, barWidth + (slot * 0.4));
        }

        w.Line(f.PlotLeft, zero, f.PlotLeft + f.PlotWidth, zero, Axis, 1);
        return w.Finish();
    }

    /// <summary>Stacked areas (positives above zero, negatives below), £ axis, legend.</summary>
    public static string StackedArea(StackedAreaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        int n = chart.XValues.Count;
        Frame f = Frame.For(chart.Width, chart.Height, legendRows: LegendRows(chart.Series.Count));
        double[] posTop = new double[n];
        double[] negTop = new double[n];
        List<(AreaSeries Series, double[] Lower, double[] Upper)> bands = [];
        foreach (AreaSeries s in chart.Series)
        {
            double[] lower = new double[n];
            double[] upper = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = i < s.Values.Count ? (double)s.Values[i] : 0d;
                if (v >= 0)
                {
                    lower[i] = posTop[i];
                    posTop[i] += v;
                    upper[i] = posTop[i];
                }
                else
                {
                    upper[i] = negTop[i];
                    negTop[i] += v;
                    lower[i] = negTop[i];
                }
            }

            bands.Add((s, lower, upper));
        }

        (Scale xs, Scale ys) = Scales(f, chart.XValues.Select(x => (double)x), posTop.Concat(negTop), includeZero: true);
        SvgWriter w = new(chart.Width, chart.Height);
        w.Title(chart.Title, f);
        w.Grid(f, xs, ys, chart.XAxisTitle, chart.YAxisTitle);
        foreach ((AreaSeries s, double[] lower, double[] upper) in bands)
        {
            if (n == 1)
            {
                double x = xs.Map((double)chart.XValues[0]);
                w.Rect(x - 8, Math.Min(ys.Map(upper[0]), ys.Map(lower[0])), 16, Math.Abs(ys.Map(upper[0]) - ys.Map(lower[0])), s.Colour, opacity: 0.85);
                continue;
            }

            List<(double, double)> path = [];
            for (int i = 0; i < n; i++)
            {
                path.Add((xs.Map((double)chart.XValues[i]), ys.Map(upper[i])));
            }

            for (int i = n - 1; i >= 0; i--)
            {
                path.Add((xs.Map((double)chart.XValues[i]), ys.Map(lower[i])));
            }

            w.Polygon(path, s.Colour, 0.85, s.Colour);
        }

        w.Line(f.PlotLeft, ys.Map(0), f.PlotLeft + f.PlotWidth, ys.Map(0), Axis, 1);
        w.Legend(f, chart.Series.Select(s => new LegendItem(s.Name, s.Colour, LegendStyle.Box)));
        return w.Finish();
    }

    /// <summary>Fan chart: P10–P90 and P25–P75 shaded, median line, legend.</summary>
    public static string Fan(FanChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        IReadOnlyList<FanPoint> pts = chart.Points;
        Frame f = Frame.For(chart.Width, chart.Height, legendRows: 1);
        IEnumerable<double> ys0 = pts.SelectMany(p => new[] { (double)p.P10, (double)p.P25, (double)p.P50, (double)p.P75, (double)p.P90 });
        (Scale xs, Scale ys) = Scales(f, pts.Select(p => (double)p.X), ys0, includeZero: true);
        SvgWriter w = new(chart.Width, chart.Height);
        w.Title(chart.Title, f);
        w.Grid(f, xs, ys, chart.XAxisTitle, chart.YAxisTitle);
        if (pts.Count == 1)
        {
            double x = xs.Map((double)pts[0].X);
            w.Line(x, ys.Map((double)pts[0].P10), x, ys.Map((double)pts[0].P90), chart.Colour, 6, opacity: 0.25);
            w.Line(x, ys.Map((double)pts[0].P25), x, ys.Map((double)pts[0].P75), chart.Colour, 6, opacity: 0.45);
            w.Circle(x, ys.Map((double)pts[0].P50), 4, chart.Colour);
        }
        else if (pts.Count > 1)
        {
            w.Polygon(Band(pts, p => (double)p.P90, p => (double)p.P10, xs, ys), chart.Colour, 0.22, null);
            w.Polygon(Band(pts, p => (double)p.P75, p => (double)p.P25, xs, ys), chart.Colour, 0.40, null);
            w.Polyline(pts.Select(p => (xs.Map((double)p.X), ys.Map((double)p.P50))), chart.Colour, 2.4, string.Empty);
        }

        w.Legend(f,
        [
            new LegendItem("10th–90th percentile", chart.Colour, LegendStyle.LightBox),
            new LegendItem("25th–75th percentile", chart.Colour, LegendStyle.MidBox),
            new LegendItem("Median", chart.Colour, LegendStyle.Line),
        ]);
        return w.Finish();
    }

    /// <summary>Formats an axis tick as £ with k/m abbreviations (£250k, £1.2m); negatives keep the sign in front.</summary>
    public static string MoneyTick(double value)
    {
        string sign = value < 0 ? "-" : string.Empty;
        double a = Math.Abs(value);
        string body = a >= 1_000_000 ? (a / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "m"
            : a >= 1_000 ? (a / 1_000).ToString("0.##", CultureInfo.InvariantCulture) + "k"
            : a.ToString("0.##", CultureInfo.InvariantCulture);
        return sign + "£" + body;
    }

    private static string MoneyFull(decimal value) => value.ToString("C0", CultureInfo.GetCultureInfo("en-GB"));

    private static int LegendRows(int items) => items == 0 ? 0 : (int)Math.Ceiling(items / 3d);

    private static (Scale X, Scale Y) Scales(Frame f, IEnumerable<double> xs, IEnumerable<double> ys, bool includeZero)
        => (Scale.Integer(xs, f.PlotLeft, f.PlotLeft + f.PlotWidth), Scale.Nice(ys, includeZero, f.PlotBottom, f.PlotTop));

    private static List<(double, double)> Band(IReadOnlyList<FanPoint> pts, Func<FanPoint, double> upper, Func<FanPoint, double> lower, Scale xs, Scale ys)
    {
        List<(double, double)> path = [];
        foreach (FanPoint p in pts)
        {
            path.Add((xs.Map((double)p.X), ys.Map(upper(p))));
        }

        for (int i = pts.Count - 1; i >= 0; i--)
        {
            path.Add((xs.Map((double)pts[i].X), ys.Map(lower(pts[i]))));
        }

        return path;
    }

    private enum LegendStyle
    {
        Line,
        DashedLine,
        Box,
        LightBox,
        MidBox,
    }

    private sealed record LegendItem(string Name, string Colour, LegendStyle Style);

    /// <summary>Plot-area geometry for a chart of a given size.</summary>
    private sealed record Frame(double PlotLeft, double PlotTop, double PlotWidth, double PlotHeight, int LegendRows, double Width, double Height)
    {
        public double PlotBottom => PlotTop + PlotHeight;

        public static Frame For(int width, int height, int legendRows, double bottomExtra = 0)
        {
            const double left = 78, right = 18, top = 34;
            double bottom = 46 + bottomExtra + (legendRows * 18);
            return new Frame(left, top, Math.Max(10, width - left - right), Math.Max(10, height - top - bottom), legendRows, width, height);
        }
    }

    /// <summary>A linear data-to-pixel mapping with "nice" tick positions.</summary>
    private sealed class Scale
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _pixelStart;
        private readonly double _pixelEnd;

        private Scale(double min, double max, double step, double pixelStart, double pixelEnd)
        {
            _min = min;
            _max = max;
            Step = step;
            _pixelStart = pixelStart;
            _pixelEnd = pixelEnd;
        }

        public double Step { get; }

        public IEnumerable<double> Ticks()
        {
            for (double t = _min; t <= _max + (Step * 1e-9); t += Step)
            {
                yield return Math.Abs(t) < Step * 1e-9 ? 0 : t;
            }
        }

        public double Map(double v) => _pixelStart + ((v - _min) / (_max - _min) * (_pixelEnd - _pixelStart));

        /// <summary>Heckbert "nice numbers" axis; degenerate ranges (single value, all zeros) are widened so something is drawn.</summary>
        public static Scale Nice(IEnumerable<double> values, bool includeZero, double pixelStart, double pixelEnd)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (double v in values)
            {
                if (double.IsFinite(v))
                {
                    min = Math.Min(min, v);
                    max = Math.Max(max, v);
                }
            }

            if (double.IsInfinity(min))
            {
                min = 0;
                max = 0;
            }

            if (includeZero)
            {
                min = Math.Min(min, 0);
                max = Math.Max(max, 0);
            }

            if (max - min <= 0)
            {
                if (max == 0)
                {
                    max = 100;
                }
                else if (max > 0)
                {
                    min = 0;
                }
                else
                {
                    max = 0;
                }
            }

            double step = NiceNumber((max - min) / 5, round: true);
            double niceMin = Math.Floor(min / step) * step;
            double niceMax = Math.Ceiling(max / step) * step;
            if (niceMax - niceMin <= 0)
            {
                niceMax = niceMin + step;
            }

            return new Scale(niceMin, niceMax, step, pixelStart, pixelEnd);
        }

        /// <summary>An x-axis of years or ages: integer ticks at a 1/2/5/10... spacing, at least one unit wide.</summary>
        public static Scale Integer(IEnumerable<double> values, double pixelStart, double pixelEnd)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (double v in values)
            {
                if (double.IsFinite(v))
                {
                    min = Math.Min(min, v);
                    max = Math.Max(max, v);
                }
            }

            if (double.IsInfinity(min))
            {
                min = 0;
                max = 1;
            }

            min = Math.Floor(min);
            max = Math.Ceiling(max);
            if (max <= min)
            {
                max = min + 1;
            }

            double step = Math.Max(1, NiceNumber((max - min) / 8, round: true));
            return new Scale(min, max, step, pixelStart, pixelEnd);
        }

        private static double NiceNumber(double range, bool round)
        {
            if (range <= 0 || !double.IsFinite(range))
            {
                return 1;
            }

            double exponent = Math.Floor(Math.Log10(range));
            double fraction = range / Math.Pow(10, exponent);
            double nice = round
                ? fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10
                : fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            return nice * Math.Pow(10, exponent);
        }
    }

    /// <summary>Minimal SVG string builder; every number is written with the invariant culture.</summary>
    private sealed class SvgWriter
    {
        private readonly StringBuilder _sb = new();
        private readonly int _width;
        private readonly int _height;

        public SvgWriter(int width, int height)
        {
            _width = width;
            _height = height;
            _sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" font-family=\"{FontFamily}\">");
            _sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"#FFFFFF\"/>");
        }

        public void Title(string title, Frame f) => Text(f.PlotLeft, 20, title, 14, "start", Ink, bold: true);

        public void Grid(Frame f, Scale xs, Scale ys, string xTitle, string yTitle)
        {
            YAxis(f, ys, yTitle);
            foreach (double t in xs.Ticks())
            {
                double x = xs.Map(t);
                Line(x, f.PlotBottom, x, f.PlotBottom + 4, Axis, 1);
                Text(x, f.PlotBottom + 16, t.ToString("0", CultureInfo.InvariantCulture), 10, "middle", Ink);
            }

            Line(f.PlotLeft, f.PlotBottom, f.PlotLeft + f.PlotWidth, f.PlotBottom, Axis, 1);
            Text(f.PlotLeft + (f.PlotWidth / 2), f.PlotBottom + 32, xTitle, 10, "middle", Ink);
        }

        public void YAxis(Frame f, Scale ys, string yTitle)
        {
            foreach (double t in ys.Ticks())
            {
                double y = ys.Map(t);
                Line(f.PlotLeft, y, f.PlotLeft + f.PlotWidth, y, Grid, 1);
                Text(f.PlotLeft - 6, y + 3.5, MoneyTick(t), 10, "end", Ink);
            }

            Line(f.PlotLeft, f.PlotTop, f.PlotLeft, f.PlotBottom, Axis, 1);
            double cx = 14, cy = f.PlotTop + (f.PlotHeight / 2);
            _sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{N(cx)}\" y=\"{N(cy)}\" font-size=\"10\" fill=\"{Ink}\" text-anchor=\"middle\" transform=\"rotate(-90 {N(cx)} {N(cy)})\">{Esc(yTitle)}</text>");
        }

        public void Legend(Frame f, IEnumerable<LegendItem> items)
        {
            double x = f.PlotLeft, y = f.PlotBottom + 46;
            int i = 0;
            foreach (LegendItem item in items)
            {
                if (i > 0 && i % 3 == 0)
                {
                    x = f.PlotLeft;
                    y += 18;
                }

                switch (item.Style)
                {
                    case LegendStyle.Line:
                        Line(x, y - 4, x + 24, y - 4, item.Colour, 2.4);
                        break;
                    case LegendStyle.DashedLine:
                        Line(x, y - 4, x + 24, y - 4, item.Colour, 2.2, dash: " stroke-dasharray=\"7 4\"");
                        break;
                    case LegendStyle.Box:
                        Rect(x, y - 10, 24, 12, item.Colour, opacity: 0.85);
                        break;
                    case LegendStyle.LightBox:
                        Rect(x, y - 10, 24, 12, item.Colour, opacity: 0.22);
                        break;
                    case LegendStyle.MidBox:
                        Rect(x, y - 10, 24, 12, item.Colour, opacity: 0.40);
                        break;
                }

                Text(x + 30, y, item.Name, 10, "start", Ink);
                x += f.PlotWidth / 3;
                i++;
            }
        }

        public void Text(double x, double y, string text, double size, string anchor, string colour, bool bold = false)
            => _sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"{N(size)}\" fill=\"{colour}\" text-anchor=\"{anchor}\"{(bold ? " font-weight=\"bold\"" : string.Empty)}>{Esc(text)}</text>");

        /// <summary>Centred label broken into lines of roughly the width available (SVG has no automatic wrapping).</summary>
        public void WrappedLabel(double cx, double y, string text, double width)
        {
            int maxChars = Math.Max(8, (int)(width / 6.0));
            List<string> lines = [];
            StringBuilder current = new();
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > maxChars)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }

            for (int i = 0; i < lines.Count; i++)
            {
                Text(cx, y + (i * 13), lines[i], 10.5, "middle", Ink);
            }
        }

        public void Line(double x1, double y1, double x2, double y2, string colour, double width, double opacity = 1, string dash = "")
            => _sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke=\"{colour}\" stroke-width=\"{N(width)}\"{(opacity < 1 ? $" stroke-opacity=\"{N(opacity)}\"" : string.Empty)}{dash}/>");

        public void Rect(double x, double y, double w, double h, string fill, double opacity = 1)
            => _sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{fill}\"{(opacity < 1 ? $" fill-opacity=\"{N(opacity)}\"" : string.Empty)}/>");

        public void Circle(double cx, double cy, double r, string fill)
            => _sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" fill=\"{fill}\"/>");

        public void Polyline(IEnumerable<(double X, double Y)> points, string colour, double width, string dash)
            => _sb.Append(CultureInfo.InvariantCulture, $"<polyline points=\"{Points(points)}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"{dash}/>");

        public void Polygon(IEnumerable<(double X, double Y)> points, string fill, double opacity, string? stroke)
            => _sb.Append(CultureInfo.InvariantCulture, $"<polygon points=\"{Points(points)}\" fill=\"{fill}\" fill-opacity=\"{N(opacity)}\" stroke=\"{stroke ?? "none"}\" stroke-width=\"0.8\"/>");

        public string Finish()
        {
            _sb.Append("</svg>");
            _ = _width + _height;
            return _sb.ToString();
        }

        private static string Points(IEnumerable<(double X, double Y)> points)
        {
            StringBuilder sb = new();
            foreach ((double x, double y) in points)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(N(x)).Append(',').Append(N(y));
            }

            return sb.ToString();
        }

        private static string N(double v) => (double.IsFinite(v) ? v : 0).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Esc(string s) => s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
