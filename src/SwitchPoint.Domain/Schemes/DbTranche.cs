using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>A slice of DB pension with its own revaluation and escalation rules (e.g. pre-97 GMP, 97–05 excess, post-05).</summary>
public sealed record DbTranche
{
    public DbTranche(string name, decimal accruedAnnualPension, RevaluationRule revaluation, EscalationRule escalation, bool isGmp = false)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        AccruedAnnualPension = Guard.NonNegative(accruedAnnualPension);
        Revaluation = Guard.NotNull(revaluation);
        Escalation = Guard.NotNull(escalation);
        IsGmp = isGmp;
    }

    public string Name { get; }

    /// <summary>Annual pension accrued at the date of leaving, before revaluation.</summary>
    public decimal AccruedAnnualPension { get; }

    public RevaluationRule Revaluation { get; }
    public EscalationRule Escalation { get; }

    /// <summary>Guaranteed Minimum Pension tranches cannot be commuted for cash.</summary>
    public bool IsGmp { get; }
}
