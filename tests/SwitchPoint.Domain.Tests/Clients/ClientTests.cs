using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Tests.Clients;

public class ClientTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("QQ123456C", "******56C")]
    [InlineData("qq 12 34 56 c", "******56C")]
    [InlineData("AB", "**")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void National_insurance_number_is_masked(string? input, string? expected) => Assert.Equal(expected, Client.Mask(input));

    [Fact]
    public void Age_is_computed_correctly_around_birthdays()
    {
        Client c = new(Guid.NewGuid(), Guid.NewGuid(), "Sam", "Taylor", new DateOnly(1970, 9, 7), Sex.Female, Now);
        Assert.Equal(55, c.AgeOn(new DateOnly(2026, 9, 6)));
        Assert.Equal(56, c.AgeOn(new DateOnly(2026, 9, 7)));
        Assert.Equal("Sam Taylor", c.FullName);
    }

    [Fact]
    public void Rejects_future_or_implausible_dates_of_birth()
    {
        Assert.Throws<DomainException>(() => new Client(Guid.NewGuid(), Guid.NewGuid(), "A", "B", new DateOnly(2030, 1, 1), Sex.Male, Now));
        Assert.Throws<DomainException>(() => new Client(Guid.NewGuid(), Guid.NewGuid(), "A", "B", new DateOnly(1850, 1, 1), Sex.Male, Now));
    }

    [Fact]
    public void Circumstances_are_range_checked()
    {
        Client c = new(Guid.NewGuid(), Guid.NewGuid(), "A", "B", new DateOnly(1980, 1, 1), Sex.Male, Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.UpdateCircumstances(MaritalStatus.Single, EmploymentStatus.Employed, 40_000m, 67, TaxRegime.RestOfUk, 9, HealthStatus.Standard, false, StatePensionForecast.Unknown, Now));
        c.UpdateCircumstances(MaritalStatus.Married, EmploymentStatus.SelfEmployed, 40_000m, 65, TaxRegime.Scotland, 5, HealthStatus.Enhanced, true, new StatePensionForecast(241.30m, 35), Now);
        Assert.Equal(TaxRegime.Scotland, c.TaxRegime);
        Assert.Equal(241.30m, c.StatePension.ForecastWeeklyAmount);
    }

    [Fact]
    public void Imported_clients_need_an_external_id()
    {
        Assert.Throws<DomainException>(() => new ExternalReference(ExternalSource.Intelliflo, null));
        Assert.Equal(ExternalSource.Manual, ExternalReference.Manual.Source);
    }
}
