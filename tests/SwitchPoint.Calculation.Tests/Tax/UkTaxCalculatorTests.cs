using SwitchPoint.Calculation.Tax;
using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Tests.Tax;

public class UkTaxCalculatorTests
{
    private static readonly UkTaxCalculator Calc = new(TaxYears.Y2026_27);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12_570, 0)]
    [InlineData(20_000, 1_486)]          // (20,000 − 12,570) × 20%
    [InlineData(50_270, 7_540)]          // 37,700 × 20%
    [InlineData(60_000, 11_432)]         // 7,540 + 9,730 × 40%
    [InlineData(100_000, 27_432)]        // 7,540 + 49,730 × 40%
    [InlineData(110_000, 33_432)]        // PA reduced by 5,000: taxable 102,430 → 7,540 + 64,730×40% = 33,432
    [InlineData(125_140, 42_516)]        // PA nil: 37,700×20% + 87,440×40% = 7,540 + 34,976
    [InlineData(150_000, 53_703)]        // + 24,860 × 45%
    public void Rest_of_uk_income_tax_on_earnings(decimal income, decimal expectedTax)
    {
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: income), TaxRegime.RestOfUk);
        Assert.Equal(expectedTax, c.IncomeTax);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12_570, 0)]
    [InlineData(16_537, 753.73)]                 // 3,967 × 19%
    [InlineData(29_526, 3_351.53)]               // + 12,989 × 20%
    [InlineData(43_662, 6_320.09)]               // + 14,136 × 21%
    [InlineData(75_000, 19_482.05)]              // + 31,338 × 42%
    [InlineData(50_000, 8_982.05)]               // + 6,338 × 42%
    public void Scottish_income_tax_on_earnings(decimal income, decimal expectedTax)
    {
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: income), TaxRegime.Scotland);
        Assert.Equal(expectedTax, Math.Round(c.IncomeTax, 2));
    }

    [Fact]
    public void Personal_allowance_tapers_one_for_two_above_100k()
    {
        Assert.Equal(12_570m, Calc.PersonalAllowanceFor(100_000m));
        Assert.Equal(7_570m, Calc.PersonalAllowanceFor(110_000m));
        Assert.Equal(0m, Calc.PersonalAllowanceFor(125_140m));
        Assert.Equal(0m, Calc.PersonalAllowanceFor(200_000m));
    }

    [Fact]
    public void Pension_contributions_reduce_adjusted_net_income_for_the_taper()
    {
        // £110k salary with £10k gross RAS contribution: ANI back to £100k so full PA; basic band extended by £10k.
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: 110_000m, GrossPensionContributions: 10_000m, ReliefAtSourceContributions: 10_000m), TaxRegime.RestOfUk);
        Assert.Equal(12_570m, c.PersonalAllowance);
        // taxable 97,430: 47,700 × 20% + 49,730 × 40% = 9,540 + 19,892
        Assert.Equal(29_432m, c.IncomeTax);
    }

    [Fact]
    public void National_insurance_class_1()
    {
        Assert.Equal(0m, Calc.NationalInsuranceFor(12_570m));
        Assert.Equal(3_016m, Calc.NationalInsuranceFor(50_270m));     // 37,700 × 8%
        Assert.Equal(3_016m + 200m, Calc.NationalInsuranceFor(60_270m)); // + 10,000 × 2%
        Assert.Equal(0m, Calc.Compute(new TaxableIncome(EarnedIncome: 40_000m, SubjectToNationalInsurance: false), TaxRegime.RestOfUk).NationalInsurance);
    }

    [Fact]
    public void Savings_starting_rate_and_psa()
    {
        // Pensioner with £10,000 pension and £8,000 interest: PA covers pension; remaining PA 2,570 covers interest;
        // starting rate band (5,000, undiminished as non-savings taxable is 0) covers 5,000; PSA 1,000 covers rest → nil.
        TaxComputation c = Calc.Compute(new TaxableIncome(PensionIncome: 10_000m, SavingsInterest: 8_000m, SubjectToNationalInsurance: false), TaxRegime.RestOfUk);
        Assert.Equal(0m, c.IncomeTax);

        // Basic-rate taxpayer with £30,000 salary and £3,000 interest: starting band fully lost, PSA £1,000, £2,000 at 20%.
        TaxComputation d = Calc.Compute(new TaxableIncome(EarnedIncome: 30_000m, SavingsInterest: 3_000m), TaxRegime.RestOfUk);
        Assert.Equal(((30_000m - 12_570m) * 0.2m) + (2_000m * 0.2m), d.IncomeTax);

        // Higher-rate taxpayer: PSA £500, rest at 40%.
        TaxComputation h = Calc.Compute(new TaxableIncome(EarnedIncome: 60_000m, SavingsInterest: 1_500m), TaxRegime.RestOfUk);
        Assert.Equal(11_432m + (1_000m * 0.4m), h.IncomeTax);
    }

    [Fact]
    public void Dividends_use_allowance_then_dividend_rates()
    {
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: 30_000m, Dividends: 5_500m), TaxRegime.RestOfUk);
        // 5,500 − 500 allowance = 5,000 × 10.75% = 537.50
        Assert.Equal(3_486m + 537.50m, c.IncomeTax);
        TaxComputation h = Calc.Compute(new TaxableIncome(EarnedIncome: 60_000m, Dividends: 10_500m), TaxRegime.RestOfUk);
        Assert.Equal(11_432m + (10_000m * 0.3575m), h.IncomeTax);
    }

    [Fact]
    public void Dividends_straddling_the_basic_rate_limit_split_across_rates()
    {
        // Salary 45,000 (taxable 32,430; 5,270 of basic band left). Dividends 10,500: 500 allowance (uses band), 4,770 at 10.75%, 5,230 at 35.75%.
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: 45_000m, Dividends: 10_500m), TaxRegime.RestOfUk);
        decimal expected = (32_430m * 0.2m) + (4_770m * 0.1075m) + (5_230m * 0.3575m);
        Assert.Equal(expected, c.IncomeTax);
    }

    [Fact]
    public void Emergency_month_one_tax_on_a_flexible_payment()
    {
        // £20,000 taxable UFPLS element: 1/12 PA = 1,047.50 free; 3,141.67 at 20%; 7,286.67 at 40%; remainder 8,524.17 at 45%.
        decimal tax = Calc.EmergencyMonthOneTax(20_000m, TaxRegime.RestOfUk);
        decimal expected = (37_700m / 12m * 0.2m) + (87_440m / 12m * 0.4m) + ((20_000m - (12_570m / 12m) - (37_700m / 12m) - (87_440m / 12m)) * 0.45m);
        Assert.Equal(Math.Round(expected, 2), Math.Round(tax, 2));
        Assert.Equal(0m, Calc.EmergencyMonthOneTax(1_000m, TaxRegime.RestOfUk));
    }

    [Theory]
    [InlineData(10_000, 0)]
    [InlineData(30_000, 0)]
    [InlineData(20_000, 15_000)]
    [InlineData(45_000, 0)]
    [InlineData(80_000, 30_000)]
    [InlineData(30_000, 95_000)]   // crosses into the PA taper (60% effective)
    public void Gross_for_net_inverts_compute(decimal netRequired, decimal otherIncome)
    {
        decimal gross = Calc.GrossForNet(netRequired, otherIncome, TaxRegime.RestOfUk);
        TaxComputation with = Calc.Compute(new TaxableIncome(PensionIncome: otherIncome + gross, SubjectToNationalInsurance: false), TaxRegime.RestOfUk);
        TaxComputation without = Calc.Compute(new TaxableIncome(PensionIncome: otherIncome, SubjectToNationalInsurance: false), TaxRegime.RestOfUk);
        decimal netReceived = gross - (with.IncomeTax - without.IncomeTax);
        Assert.InRange(Math.Abs(netReceived - netRequired), 0m, 0.01m);
    }

    [Fact]
    public void Marginal_rate_reflects_the_taper_trap()
    {
        Assert.Equal(0m, Calc.MarginalRateAt(10_000m, TaxRegime.RestOfUk));
        Assert.Equal(0.2m, Calc.MarginalRateAt(30_000m, TaxRegime.RestOfUk));
        Assert.Equal(0.4m, Calc.MarginalRateAt(80_000m, TaxRegime.RestOfUk));
        Assert.Equal(0.6m, Calc.MarginalRateAt(110_000m, TaxRegime.RestOfUk));
        Assert.Equal(0.45m, Calc.MarginalRateAt(130_000m, TaxRegime.RestOfUk));
        Assert.Equal(0.42m, Calc.MarginalRateAt(50_000m, TaxRegime.Scotland));
    }

    [Fact]
    public void Lines_sum_to_tax_and_negative_inputs_are_rejected()
    {
        TaxComputation c = Calc.Compute(new TaxableIncome(EarnedIncome: 70_000m, SavingsInterest: 2_000m, Dividends: 3_000m), TaxRegime.RestOfUk);
        Assert.Equal(c.IncomeTax, c.Lines.Sum(l => l.Tax));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calc.Compute(new TaxableIncome(EarnedIncome: -1m), TaxRegime.RestOfUk));
        Assert.Throws<ArgumentException>(() => Calc.Compute(new TaxableIncome(EarnedIncome: 1m, GrossPensionContributions: 1m, ReliefAtSourceContributions: 2m), TaxRegime.RestOfUk));
    }

    [Fact]
    public void Tax_year_registry_and_2027_savings_rates()
    {
        Assert.Equal("2026/27", TaxYears.For(new DateOnly(2026, 9, 6)).Name);
        Assert.Equal("2026/27", TaxYears.For(new DateOnly(2027, 4, 5)).Name);
        Assert.Equal("2027/28", TaxYears.For(new DateOnly(2027, 4, 6)).Name);
        Assert.Equal("2027/28", TaxYears.For(new DateOnly(2035, 1, 1)).Name);
        Assert.Equal(0.22m, TaxYears.Y2027_28.SavingsBands[0].Rate);
        Assert.Equal(55, TaxYears.Y2026_27.NormalMinimumPensionAgeOn(new DateOnly(2028, 4, 5)));
        Assert.Equal(57, TaxYears.Y2026_27.NormalMinimumPensionAgeOn(new DateOnly(2028, 4, 6)));
        Assert.Throws<KeyNotFoundException>(() => TaxYears.Get("1999/00"));
    }
}
