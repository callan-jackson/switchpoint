using SwitchPoint.Calculation.Tax;

namespace SwitchPoint.Calculation.Tests.Tax;

public class PensionAllowanceCalculatorTests
{
    private static readonly PensionAllowanceCalculator Calc = new(TaxYears.Y2026_27);

    [Theory]
    [InlineData(150_000, 200_000, 60_000)]
    [InlineData(200_000, 260_000, 60_000)]   // at the thresholds: not tapered
    [InlineData(200_001, 260_001, 59_999.5)]
    [InlineData(250_000, 300_000, 40_000)]   // (300k − 260k) / 2 = 20k reduction
    [InlineData(300_000, 360_000, 10_000)]   // fully tapered
    [InlineData(500_000, 900_000, 10_000)]
    [InlineData(190_000, 300_000, 60_000)]   // threshold income test not met
    public void Tapered_annual_allowance(decimal threshold, decimal adjusted, decimal expected) =>
        Assert.Equal(expected, Calc.TaperedAnnualAllowance(threshold, adjusted));

    [Fact]
    public void Carry_forward_absorbs_excess_oldest_first()
    {
        AllowanceCheckResult r = Calc.Check(new AllowanceCheckRequest(80_000m, 100_000m, 90_000m, 90_000m, false, [5_000m, 10_000m, 20_000m]));
        Assert.Equal(60_000m, r.AnnualAllowance);
        Assert.Equal(35_000m, r.CarryForwardAvailable);
        Assert.Equal(30_000m, r.CarryForwardUsed);
        Assert.Equal(0m, r.ExcessOverAnnualAllowance);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Excess_beyond_carry_forward_is_reported()
    {
        AllowanceCheckResult r = Calc.Check(new AllowanceCheckRequest(80_000m, 100_000m, 100_000m, 100_000m, false, [5_000m]));
        Assert.Equal(35_000m, r.ExcessOverAnnualAllowance);
        Assert.Contains(r.Warnings, w => w.Contains("exceeds", StringComparison.Ordinal));
    }

    [Fact]
    public void Mpaa_applies_to_money_purchase_input_without_carry_forward()
    {
        AllowanceCheckResult r = Calc.Check(new AllowanceCheckRequest(50_000m, 70_000m, 25_000m, 15_000m, true, [40_000m, 40_000m, 40_000m]));
        Assert.Equal(5_000m, r.ExcessOverMpaa);
        Assert.Equal(0m, r.ExcessOverAnnualAllowance); // 10k DB accrual vs alternative AA 50k
        Assert.Contains(r.Warnings, w => w.Contains("MPAA", StringComparison.Ordinal));
    }

    [Fact]
    public void Relief_at_source_grossing_and_relievable_cap()
    {
        Assert.Equal(3_600m, Calc.GrossUpReliefAtSource(2_880m));
        Assert.Equal(3_600m, Calc.MaxRelievableContribution(0m));
        Assert.Equal(3_600m, Calc.MaxRelievableContribution(2_000m));
        Assert.Equal(45_000m, Calc.MaxRelievableContribution(45_000m));
    }
}
