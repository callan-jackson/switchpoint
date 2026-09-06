namespace SwitchPoint.Domain.Common;

/// <summary>Small decimal helpers shared by domain value objects. Heavy numerics live in the Calculation layer.</summary>
internal static class Arithmetic
{
    /// <summary>Returns (1 + rate)^periods using exact decimal multiplication.</summary>
    public static decimal CompoundFactor(decimal rate, int periods)
    {
        Guard.NonNegative(periods);
        var factor = 1m;
        var growth = 1m + rate;
        for (var i = 0; i < periods; i++)
        {
            factor *= growth;
        }

        return factor;
    }
}
