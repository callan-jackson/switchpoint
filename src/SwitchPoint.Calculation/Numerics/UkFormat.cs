using System.Globalization;

namespace SwitchPoint.Calculation.Numerics;

/// <summary>
/// Formatting for text the engines produce for UK advisers and their clients (regulatory wording, warnings).
/// The culture is pinned to en-GB so a report reads the same whatever culture the server runs under: on a Linux
/// container the invariant culture writes "5.0 %" and "£1,234" differently from a UK desktop, and prescribed
/// wording must not vary by host.
/// </summary>
public static class UkFormat
{
    /// <summary>en-GB, used for every client-facing string produced by the calculation engines.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>A rate held as a fraction, written as a percentage: 0.05m → "5.0%".</summary>
    public static string Percent(decimal fraction, int decimals = 1) =>
        (fraction * 100m).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), Culture) + "%";

    /// <summary>Money to the nearest pound: 1234.56m → "£1,235".</summary>
    public static string Money(decimal value) => value.ToString("C0", Culture);

    /// <summary>Money without the currency symbol, to the nearest pound.</summary>
    public static string Amount(decimal value) => value.ToString("N0", Culture);
}
