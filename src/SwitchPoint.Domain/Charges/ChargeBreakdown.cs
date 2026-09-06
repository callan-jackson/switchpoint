namespace SwitchPoint.Domain.Charges;

/// <summary>
/// Annual charges in pounds for one plan year at a given fund value, by component. <see cref="Discounts"/>
/// is zero or negative; <see cref="Total"/> is the signed sum of every component.
/// </summary>
public sealed record ChargeBreakdown(
    decimal FundValue,
    int Year,
    decimal Platform,
    decimal Product,
    decimal Fund,
    decimal Transaction,
    decimal AdviserOngoing,
    decimal Fixed,
    decimal Dealing,
    decimal Switch,
    decimal Discounts)
{
    public decimal Total => Platform + Product + Fund + Transaction + AdviserOngoing + Fixed + Dealing + Switch + Discounts;

    /// <summary>Total divided by fund value, or zero when the fund value is zero.</summary>
    public decimal EffectiveRate => FundValue == 0m ? 0m : Total / FundValue;

    public static ChargeBreakdown Zero(decimal fundValue, int year) => new(fundValue, year, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
}
