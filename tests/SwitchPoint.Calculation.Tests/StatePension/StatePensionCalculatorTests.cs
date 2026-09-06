using SwitchPoint.Calculation.StatePension;
using SwitchPoint.Calculation.Tax;

namespace SwitchPoint.Calculation.Tests.StatePension;

public class StatePensionCalculatorTests
{
    [Theory]
    [InlineData("1959-12-31", 66, 0, "2025-12-31")]
    [InlineData("1960-04-05", 66, 0, "2026-04-05")]
    [InlineData("1960-04-06", 66, 1, "2026-05-06")]
    [InlineData("1960-05-05", 66, 1, "2026-06-05")]
    [InlineData("1960-05-06", 66, 2, "2026-07-06")]
    [InlineData("1961-02-06", 66, 11, "2028-01-06")]
    [InlineData("1961-03-05", 66, 11, "2028-02-05")]
    [InlineData("1961-03-06", 67, 0, "2028-03-06")]
    [InlineData("1970-09-07", 67, 0, "2037-09-07")]
    [InlineData("1977-04-05", 67, 0, "2044-04-05")]
    [InlineData("1977-04-06", 67, 1, "2044-05-06")]
    [InlineData("1978-03-05", 67, 11, "2046-02-05")]
    [InlineData("1978-03-06", 68, 0, "2046-03-06")]
    [InlineData("1990-01-01", 68, 0, "2058-01-01")]
    public void State_pension_age_follows_the_gov_uk_timetable(string dob, int years, int months, string reached)
    {
        StatePensionAge spa = StatePensionCalculator.StatePensionAgeFor(DateOnly.Parse(dob, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(years, spa.Years);
        Assert.Equal(months, spa.Months);
        Assert.Equal(DateOnly.Parse(reached, System.Globalization.CultureInfo.InvariantCulture), spa.ReachedOn);
    }

    [Fact]
    public void Age_68_is_flagged_as_under_review()
    {
        Assert.True(StatePensionCalculator.StatePensionAgeFor(new DateOnly(1980, 1, 1)).SubjectToReview);
        Assert.False(StatePensionCalculator.StatePensionAgeFor(new DateOnly(1970, 1, 1)).SubjectToReview);
    }

    [Fact]
    public void Leap_day_births_roll_forward()
    {
        StatePensionAge spa = StatePensionCalculator.StatePensionAgeFor(new DateOnly(1964, 2, 29));
        Assert.Equal(new DateOnly(2031, 3, 1), spa.ReachedOn);
    }

    [Fact]
    public void Entitlement_prefers_forecast_then_pro_rata()
    {
        StatePensionCalculator c = new(TaxYears.Y2026_27);
        Assert.Equal(241.30m * 52m, c.AnnualEntitlement(null, 35));
        Assert.Equal(241.30m * 52m, c.AnnualEntitlement(null, 40));
        Assert.InRange(Math.Abs((241.30m * 52m * 20m / 35m) - c.AnnualEntitlement(null, 20)), 0m, 0.0001m);
        Assert.Equal(0m, c.AnnualEntitlement(null, 9));
        Assert.Equal(0m, c.AnnualEntitlement(null, null));
        Assert.Equal(200m * 52m, c.AnnualEntitlement(200m, 35));
    }
}
