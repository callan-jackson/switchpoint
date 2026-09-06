using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>Per-deal charges for buying or selling funds and exchange-traded instruments, with the expected number of deals a year.</summary>
public sealed record DealingCharges
{
    public DealingCharges(decimal fundDealAmount = 0m, decimal etfDealAmount = 0m, int expectedFundDealsPerYear = 0, int expectedEtfDealsPerYear = 0)
    {
        FundDealAmount = Guard.NonNegative(fundDealAmount);
        EtfDealAmount = Guard.NonNegative(etfDealAmount);
        ExpectedFundDealsPerYear = Guard.NonNegative(expectedFundDealsPerYear);
        ExpectedEtfDealsPerYear = Guard.NonNegative(expectedEtfDealsPerYear);
    }

    public decimal FundDealAmount { get; }

    public decimal EtfDealAmount { get; }

    public int ExpectedFundDealsPerYear { get; }

    public int ExpectedEtfDealsPerYear { get; }

    public int ExpectedDealsPerYear => ExpectedFundDealsPerYear + ExpectedEtfDealsPerYear;

    public static DealingCharges None { get; } = new();

    /// <summary>Expected dealing cost in pounds per year.</summary>
    public decimal AnnualCost => FundDealAmount * ExpectedFundDealsPerYear + EtfDealAmount * ExpectedEtfDealsPerYear;
}
