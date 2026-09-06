using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Calculation.CriticalYield;

/// <summary>A ceding (existing) scheme as the engine sees it.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Projection">Projection of the scheme if retained: current value, its own charges and continuing contributions, no exit penalty.</param>
/// <param name="NetTransferValue">Transfer value after the scheme's exit penalty and any MVR, i.e. what actually moves.</param>
/// <param name="Guarantees">Valuable features that would be lost on transfer.</param>
public sealed record CedingSchemeInput(string Name, ProjectionRequest Projection, decimal NetTransferValue, Guarantees Guarantees);

/// <summary>Inputs for a switching analysis. See docs/methodology/critical-yield.md.</summary>
/// <param name="CedingSchemes">One or more schemes being considered for transfer.</param>
/// <param name="ReceivingTemplate">
/// Projection template for the receiving product: its charge schedule, contributions (the redirected contributions),
/// term and inflation. StartValue and GrowthRate are set by the calculator.
/// </param>
/// <param name="GrowthLower">COBS 13 lower rate (nominal).</param>
/// <param name="GrowthIntermediate">COBS 13 intermediate rate (nominal): the headline comparison.</param>
/// <param name="GrowthHigher">COBS 13 higher rate (nominal).</param>
/// <param name="Inflation">Price inflation for real-terms figures.</param>
public sealed record CriticalYieldRequest(
    IReadOnlyList<CedingSchemeInput> CedingSchemes,
    ProjectionRequest ReceivingTemplate,
    decimal GrowthLower,
    decimal GrowthIntermediate,
    decimal GrowthHigher,
    decimal Inflation);

/// <summary>Verdict for a single ceding scheme, mapped to the FSA 2009 switching template outcomes.</summary>
public enum SwitchVerdict
{
    /// <summary>The receiving product is cheaper and the switch is expected to be ahead at the assumed growth rate.</summary>
    SwitchCandidate = 0,
    /// <summary>Charges are within 0.1% RIY of each other; the decision turns on non-cost factors.</summary>
    Consider = 1,
    /// <summary>The receiving product is dearer; switching needs a documented non-cost reason.</summary>
    Retain = 2,
    /// <summary>The scheme has guarantees or protected features; it must be referred rather than auto-verdicted.</summary>
    Refer = 3,
}

/// <summary>Per-scheme figures at the intermediate rate.</summary>
public sealed record CedingSchemeResult(
    string Name,
    decimal CurrentValue,
    decimal NetTransferValue,
    decimal ProjectedValueIfRetained,
    RiyResult RiyIfRetained,
    RiyResult RiyIfSwitched,
    decimal CriticalYieldAlone,
    bool GuaranteesFlagged,
    SwitchVerdict Verdict);

/// <summary>Critical yield figures at one growth rate.</summary>
/// <param name="GrowthRate">Assumed nominal growth rate g for the ceding schemes.</param>
/// <param name="ExistingValueAtRetirement">Σ projected values of the ceding schemes if retained.</param>
/// <param name="ReceivingValueAtRetirement">Receiving product projected at g.</param>
/// <param name="CriticalYield">x*: growth the receiving product needs to match the existing value.</param>
/// <param name="Headroom">g − x*.</param>
/// <param name="ProjectedGain">Receiving value minus existing value at g.</param>
/// <param name="BreakEvenYear">First plan year at which the receiving product's value is at least the existing value; null if never within the term.</param>
public sealed record CriticalYieldAtRate(
    decimal GrowthRate,
    decimal ExistingValueAtRetirement,
    decimal ReceivingValueAtRetirement,
    decimal CriticalYield,
    decimal CriticalYieldReal,
    decimal Headroom,
    decimal ProjectedGain,
    int? BreakEvenYear,
    bool Converged);

/// <summary>Output of the switching analysis.</summary>
public sealed record CriticalYieldResult(
    CriticalYieldAtRate Lower,
    CriticalYieldAtRate Intermediate,
    CriticalYieldAtRate Higher,
    IReadOnlyList<CedingSchemeResult> Schemes,
    RiyResult ReceivingRiy,
    decimal TotalNetTransferValue,
    decimal InitialAdviserCharge,
    IReadOnlyList<string> Warnings)
{
    public bool AnyGuaranteesFlagged => Schemes.Any(s => s.GuaranteesFlagged);
}

/// <summary>
/// Pension switching critical yield: the growth rate a receiving product must earn, before its charges, for its
/// value at retirement to equal the projected value of the ceding schemes if left where they are.
/// See docs/methodology/critical-yield.md.
/// </summary>
public sealed class CriticalYieldCalculator
{
    private const decimal RiyIndifferenceBand = 0.001m; // 0.1%
    private readonly ProjectionEngine _engine;
    private readonly ReductionInYieldCalculator _riy;

    public CriticalYieldCalculator(ProjectionEngine engine, ReductionInYieldCalculator riy)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _riy = riy ?? throw new ArgumentNullException(nameof(riy));
    }

    public CriticalYieldResult Calculate(CriticalYieldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        decimal totalTransfer = request.CedingSchemes.Sum(s => s.NetTransferValue);
        ProjectionRequest receivingAtIntermediate = Receiving(request, totalTransfer, request.GrowthIntermediate);
        ProjectionResult receivingProjection = _engine.Project(receivingAtIntermediate);
        RiyResult receivingRiy = _riy.Calculate(receivingAtIntermediate);
        List<string> warnings = [];

        CriticalYieldAtRate lower = AtRate(request, totalTransfer, request.GrowthLower);
        CriticalYieldAtRate intermediate = AtRate(request, totalTransfer, request.GrowthIntermediate);
        CriticalYieldAtRate higher = AtRate(request, totalTransfer, request.GrowthHigher);

        List<CedingSchemeResult> schemes = [];
        foreach (CedingSchemeInput s in request.CedingSchemes)
        {
            ProjectionRequest retained = s.Projection with { GrowthRate = request.GrowthIntermediate, ApplyExitPenaltyAtEnd = false };
            ProjectionResult retainedProjection = _engine.Project(retained);
            RiyResult riyRetained = _riy.Calculate(retained);

            // The same scheme switched on its own: its net transfer value into the receiving product with its own contributions.
            ProjectionRequest switchedAlone = request.ReceivingTemplate with
            {
                StartValue = s.NetTransferValue,
                GrowthRate = request.GrowthIntermediate,
                Contributions = s.Projection.Contributions,
            };
            RiyResult riySwitched = _riy.Calculate(switchedAlone);
            decimal cyAlone = SolveCriticalYield(switchedAlone, retainedProjection.FinalValue, request.GrowthIntermediate, out bool converged);
            if (!converged)
            {
                warnings.Add($"Critical yield for '{s.Name}' did not converge; the figure is indicative.");
            }

            bool flagged = s.Guarantees.RequiresReferral;
            SwitchVerdict verdict = Verdict(flagged, riyRetained.TotalRiy, riySwitched.TotalRiy, request.GrowthIntermediate - cyAlone);
            schemes.Add(new CedingSchemeResult(s.Name, s.Projection.StartValue, s.NetTransferValue, retainedProjection.FinalValue, riyRetained, riySwitched, cyAlone, flagged, verdict));
            if (flagged)
            {
                warnings.Add($"'{s.Name}' has guarantees or protected features (FSA switching outcome 2); it is referred for adviser judgement.");
            }
        }

        if (intermediate.Headroom < 0m)
        {
            warnings.Add("At the intermediate rate the receiving product must outperform the existing arrangements to match them (negative headroom); a switch on cost grounds needs a documented non-cost reason (FSA switching outcome 1).");
        }

        return new CriticalYieldResult(lower, intermediate, higher, schemes, receivingRiy, totalTransfer, receivingProjection.InitialAdviserCharge, warnings);
    }

    private CriticalYieldAtRate AtRate(CriticalYieldRequest request, decimal totalTransfer, decimal g)
    {
        List<ProjectionResult> retained = [.. request.CedingSchemes.Select(s => _engine.Project(s.Projection with { GrowthRate = g, ApplyExitPenaltyAtEnd = false }))];
        decimal existing = retained.Sum(p => p.FinalValue);
        ProjectionRequest receivingRequest = Receiving(request, totalTransfer, g);
        ProjectionResult receiving = _engine.Project(receivingRequest);
        decimal cy = SolveCriticalYield(receivingRequest, existing, g, out bool converged);
        int? breakEven = BreakEvenYear(retained, receiving);
        return new CriticalYieldAtRate(g, existing, receiving.FinalValue, cy, RateMath.Real(cy, request.Inflation), g - cy, receiving.FinalValue - existing, breakEven, converged);
    }

    private decimal SolveCriticalYield(ProjectionRequest receiving, decimal target, decimal g, out bool converged)
    {
        converged = true;
        if (target <= 0m)
        {
            return -0.99m;
        }

        try
        {
            RootResult root = RootFinder.Solve(x => _engine.Project(receiving with { GrowthRate = Math.Max(-0.99m, x) }).FinalValue - target, g - 0.10m, g + 0.10m, RootOptions.Default);
            return root.Value;
        }
        catch (RootNotBracketedException)
        {
            converged = false;
            return g;
        }
        catch (RootNotConvergedException)
        {
            converged = false;
            return g;
        }
    }

    private static ProjectionRequest Receiving(CriticalYieldRequest request, decimal totalTransfer, decimal g)
    {
        // Contributions: the template's own list if supplied, otherwise every ceding scheme's continuing contributions redirected.
        IReadOnlyList<ContributionSpec> contributions = request.ReceivingTemplate.Contributions.Count > 0
            ? request.ReceivingTemplate.Contributions
            : [.. request.CedingSchemes.SelectMany(s => s.Projection.Contributions)];
        return request.ReceivingTemplate with { StartValue = totalTransfer, GrowthRate = g, Contributions = contributions };
    }

    private static int? BreakEvenYear(IReadOnlyList<ProjectionResult> retained, ProjectionResult receiving)
    {
        foreach (ProjectionRow row in receiving.Schedule.Where(r => r.Month % 12 == 0))
        {
            decimal existing = retained.Sum(p => p.AtYear(row.Year).Value);
            if (row.Value >= existing)
            {
                return row.Year;
            }
        }

        return null;
    }

    private static SwitchVerdict Verdict(bool flagged, decimal riyRetained, decimal riySwitched, decimal headroom)
    {
        if (flagged)
        {
            return SwitchVerdict.Refer;
        }

        decimal diff = riySwitched - riyRetained;
        if (Math.Abs(diff) <= RiyIndifferenceBand)
        {
            return SwitchVerdict.Consider;
        }

        return diff < 0m && headroom >= 0m ? SwitchVerdict.SwitchCandidate : SwitchVerdict.Retain;
    }

    private static void Validate(CriticalYieldRequest r)
    {
        ArgumentNullException.ThrowIfNull(r.CedingSchemes);
        ArgumentNullException.ThrowIfNull(r.ReceivingTemplate);
        if (r.CedingSchemes.Count == 0)
        {
            throw new ArgumentException("At least one ceding scheme is required.", nameof(r));
        }

        if (!(r.GrowthLower <= r.GrowthIntermediate && r.GrowthIntermediate <= r.GrowthHigher))
        {
            throw new ArgumentException("Growth rates must be ordered lower ≤ intermediate ≤ higher.", nameof(r));
        }

        if (r.ReceivingTemplate.Months < 1)
        {
            throw new ArgumentException("The receiving projection needs a term of at least one month.", nameof(r));
        }

        foreach (CedingSchemeInput s in r.CedingSchemes)
        {
            if (s.NetTransferValue < 0m)
            {
                throw new ArgumentException($"Net transfer value for '{s.Name}' cannot be negative.", nameof(r));
            }

            if (s.Projection.Months != r.ReceivingTemplate.Months)
            {
                throw new ArgumentException($"Ceding scheme '{s.Name}' must be projected over the same term as the receiving product.", nameof(r));
            }
        }
    }
}
