using System.Globalization;
using System.Text;

namespace SwitchPoint.Reports.Composition;

/// <summary>en-GB presentation formatting shared by every report: £12,345; 5.25%; 6 September 2026.</summary>
public static class Fmt
{
    /// <summary>The culture used for every number and date on a report.</summary>
    public static CultureInfo Gb { get; } = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>£12,345 (whole pounds, sign in front: -£1,234).</summary>
    public static string Money(decimal value) => value.ToString("C0", Gb);

    /// <summary>£12,345.67.</summary>
    public static string Money2(decimal value) => value.ToString("C2", Gb);

    /// <summary>£12,345 or "n/a" when absent.</summary>
    public static string Money(decimal? value) => value is { } v ? Money(v) : "n/a";

    /// <summary>5.25% — the value is already a percentage (5.25m), not a fraction.</summary>
    public static string Pct(decimal valuePct, int decimals = 2) => valuePct.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), Gb) + "%";

    /// <summary>5.25% or "n/a" when absent.</summary>
    public static string Pct(decimal? valuePct, int decimals = 2) => valuePct is { } v ? Pct(v, decimals) : "n/a";

    /// <summary>Signed percentage points: +0.35% / -0.10%.</summary>
    public static string SignedPct(decimal valuePct, int decimals = 2) => (valuePct > 0 ? "+" : string.Empty) + Pct(valuePct, decimals);

    /// <summary>A plain number with thousands separators.</summary>
    public static string Num(decimal value, int decimals = 0) => value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), Gb);

    public static string Int(int value) => value.ToString("N0", Gb);

    public static string Int(int? value) => value is { } v ? Int(v) : "n/a";

    /// <summary>6 September 2026.</summary>
    public static string Date(DateOnly date) => date.ToString("d MMMM yyyy", Gb);

    public static string Date(DateOnly? date) => date is { } d ? Date(d) : "n/a";

    /// <summary>6 September 2026 at 14:05 UTC.</summary>
    public static string Stamp(DateTime utc) => utc.ToString("d MMMM yyyy 'at' HH:mm 'UTC'", Gb);

    public static string Stamp(DateTime? utc) => utc is { } d ? Stamp(d) : "n/a";

    public static string YesNo(bool value) => value ? "Yes" : "No";

    /// <summary>Splits a PascalCase enum name into words: SwitchCandidate → "Switch candidate"; keeps known acronyms.</summary>
    public static string Words(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Words(value.ToString());
    }

    public static string Words(string pascal)
    {
        ArgumentNullException.ThrowIfNull(pascal);
        StringBuilder sb = new(pascal.Length + 8);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(pascal[i - 1]))
            {
                sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (i > 0 && char.IsUpper(c) && i + 1 < pascal.Length && char.IsLower(pascal[i + 1]))
            {
                sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Replace("Uk", "UK", StringComparison.Ordinal)
            .Replace(" isa", " ISA", StringComparison.Ordinal)
            .Replace("Isa", "ISA", StringComparison.Ordinal)
            .Replace("Sipp", "SIPP", StringComparison.Ordinal)
            .Replace(" pcls", " PCLS", StringComparison.Ordinal)
            .Replace("Pcls", "PCLS", StringComparison.Ordinal)
            .Replace(" ufpls", " UFPLS", StringComparison.Ordinal)
            .Replace(" cpi", " CPI", StringComparison.Ordinal)
            .Replace(" rpi", " RPI", StringComparison.Ordinal);
    }

    /// <summary>Text or an en dash when empty, so table cells never render blank.</summary>
    public static string OrDash(string? text) => string.IsNullOrWhiteSpace(text) ? "–" : text;

    public static KeyValuePair<string, string> Kv(string key, string value) => new(key, value);
}
