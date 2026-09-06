using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Domain.Tests.Analysis;

public class AnalysisLifecycleTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    private static PensionSwitchAnalysis NewSwitch() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Consolidate three plans", 67, Now);

    [Fact]
    public void Starts_as_draft_with_version_zero()
    {
        PensionSwitchAnalysis a = NewSwitch();
        Assert.Equal(AnalysisStatus.Draft, a.Status);
        Assert.Equal(0, a.Version);
        Assert.False(a.IsReadyToCalculate);
    }

    [Fact]
    public void Recording_a_result_bumps_version_and_status()
    {
        PensionSwitchAnalysis a = NewSwitch();
        a.RecordResult("{\"criticalYield\":0.031}", new string('a', 64), "1.0.0", Now);
        Assert.Equal(AnalysisStatus.Calculated, a.Status);
        Assert.Equal(1, a.Version);
        a.RecordResult("{\"criticalYield\":0.032}", new string('b', 64), "1.0.0", Now.AddMinutes(1));
        Assert.Equal(2, a.Version);
    }

    [Fact]
    public void Changing_inputs_returns_calculated_analysis_to_draft()
    {
        PensionSwitchAnalysis a = NewSwitch();
        a.RecordResult("{}", new string('a', 64), "1.0.0", Now);
        a.SetRetirementAge(65, Now);
        Assert.Equal(AnalysisStatus.Draft, a.Status);
        Assert.Equal(1, a.Version);
    }

    [Fact]
    public void Locked_analysis_rejects_every_mutation()
    {
        PensionSwitchAnalysis a = NewSwitch();
        a.RecordResult("{}", new string('a', 64), "1.0.0", Now);
        a.Lock(Now);
        Assert.True(a.IsLocked);
        Assert.Throws<DomainException>(() => a.SetRetirementAge(60, Now));
        Assert.Throws<DomainException>(() => a.SetCedingSchemes([Guid.NewGuid()], Now));
        Assert.Throws<DomainException>(() => a.RecordResult("{}", new string('c', 64), "1.0.0", Now));
        Assert.Throws<DomainException>(() => a.Rename("x", Now));
        Assert.Throws<DomainException>(() => a.SetRationale("x", Now));
    }

    [Fact]
    public void Only_calculated_analyses_can_be_locked() =>
        Assert.Throws<DomainException>(() => NewSwitch().Lock(Now));

    [Fact]
    public void Proposal_requires_holdings_or_model_portfolio_and_weights_summing_to_one()
    {
        PensionSwitchAnalysis a = NewSwitch();
        Assert.Throws<DomainException>(() => a.SetProposal(Guid.NewGuid(), 1, [], null, AdviserCharge.None, Now));
        Assert.Throws<DomainException>(() => a.SetProposal(Guid.NewGuid(), 1, [new Holding("A", 0.6m), new Holding("B", 0.3m)], null, AdviserCharge.None, Now));
        a.SetProposal(Guid.NewGuid(), 1, [new Holding("A", 0.6m), new Holding("B", 0.4m)], null, AdviserCharge.None, Now);
        a.SetCedingSchemes([Guid.NewGuid(), Guid.NewGuid()], Now);
        Assert.True(a.IsReadyToCalculate);
    }

    [Fact]
    public void Db_transfer_contingent_charging_requires_carve_out()
    {
        DbTransferAnalysis d = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1), Now);
        Assert.Throws<DomainException>(() => d.SetChargeBasis(AdviserChargeBasis.Contingent, null, Now));
        d.SetChargeBasis(AdviserChargeBasis.Contingent, "Serious ill-health (COBS 19.1B.9R(2)(a))", Now);
        Assert.Equal(AdviserChargeBasis.Contingent, d.ChargeBasis);
        d.SetChargeBasis(AdviserChargeBasis.NonContingent, "ignored", Now);
        Assert.Null(d.ContingentChargingCarveOut);
    }

    [Fact]
    public void Cashflow_plan_validates_components()
    {
        CashflowPlan p = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Retirement plan", Now);
        Assert.Throws<DomainException>(() => p.ReplaceIncomes([new PlanIncome("Salary", IncomeKind.Employment, 50_000m, 60, 55, 0.035m)], Now));
        Assert.Throws<DomainException>(() => p.SetStrategy(PlanStrategy.Default with { WithdrawalOrder = [PlanAssetKind.Cash, PlanAssetKind.Cash] }, Now));
        p.ReplaceAssets([new PlanAsset("SIPP", PlanAssetKind.UncrystallisedPension, 250_000m, 0.05m, ChargeSchedule.None)], Now);
        p.SetStochasticSettings(42, 2000, Now);
        Assert.Equal(2000, p.StochasticPaths);
        Assert.Equal((ulong)42, p.StochasticSeed);
        Assert.Throws<ArgumentOutOfRangeException>(() => p.SetStochasticSettings(1, 50, Now));
    }
}
