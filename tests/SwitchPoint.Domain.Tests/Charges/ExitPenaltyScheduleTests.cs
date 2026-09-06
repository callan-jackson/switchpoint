using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Tests.Charges;

public class ExitPenaltyScheduleTests
{
    private static readonly ExitPenaltySchedule Declining = ExitPenaltySchedule.Declining((1m, 0.05m), (2m, 0.04m), (3m, 0.03m), (4m, 0.02m), (5m, 0.01m));

    [Theory]
    [InlineData(0, 5000)]
    [InlineData(0.99, 5000)]
    [InlineData(1, 4000)]      // 'until 1 year' is exclusive: at exactly one year the next band applies
    [InlineData(4.5, 1000)]
    [InlineData(5, 0)]         // schedule expired
    [InlineData(30, 0)]
    public void Declining_percentage_bands(decimal years, decimal expected) =>
        Assert.Equal(expected, Declining.PenaltyFor(years, 100_000m));

    [Fact]
    public void Fixed_amount_penalty()
    {
        ExitPenaltySchedule s = ExitPenaltySchedule.FixedAmount(75m, null);
        Assert.Equal(75m, s.PenaltyFor(0m, 10_000m));
        Assert.Equal(75m, s.PenaltyFor(40m, 10_000m));
        Assert.Null(s.ExpiresAfterYears);
    }

    [Fact]
    public void Combined_percentage_and_amount()
    {
        ExitPenaltySchedule s = new([new ExitPenaltyBand(3m, 0.02m, 50m), new ExitPenaltyBand(null, 0m, 25m)]);
        Assert.Equal(2050m, s.PenaltyFor(1m, 100_000m));
        Assert.Equal(25m, s.PenaltyFor(3m, 100_000m));
    }

    [Fact]
    public void Penalty_never_exceeds_value_for_percentage_only()
    {
        ExitPenaltySchedule s = ExitPenaltySchedule.Percentage(1m, 2m);
        Assert.Equal(500m, s.PenaltyFor(0m, 500m));
    }

    [Fact]
    public void None_has_no_bands_and_zero_penalty()
    {
        Assert.True(ExitPenaltySchedule.None.IsNone);
        Assert.Equal(0m, ExitPenaltySchedule.None.PenaltyFor(0m, 1_000_000m));
        Assert.Equal(0m, ExitPenaltySchedule.None.ExpiresAfterYears);
    }

    [Fact]
    public void Rejects_non_ascending_or_open_middle_bands()
    {
        Assert.Throws<DomainException>(() => ExitPenaltySchedule.Declining((2m, 0.05m), (1m, 0.04m)));
        Assert.Throws<DomainException>(() => ExitPenaltySchedule.Declining((null, 0.05m), (1m, 0.04m)));
    }

    [Fact]
    public void Rejects_negative_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Declining.PenaltyFor(-1m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Declining.PenaltyFor(1m, -100m));
    }
}
