using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Analysis;

/// <summary>Common lifecycle for every analysis: draft → calculated → locked, with a result snapshot and version counter.</summary>
public abstract class AnalysisBase : Entity, ITenantScoped
{
    /// <summary>For EF Core materialisation only.</summary>
    protected AnalysisBase()
    {
    }

    protected AnalysisBase(Guid id, Guid firmId, Guid clientId, Guid assumptionSetId, Guid createdBy, DateTime createdAtUtc)
        : base(id, createdAtUtc)
    {
        FirmId = Guard.NotEmpty(firmId);
        ClientId = Guard.NotEmpty(clientId);
        AssumptionSetId = Guard.NotEmpty(assumptionSetId);
        CreatedBy = Guard.NotEmpty(createdBy);
    }

    public Guid FirmId { get; }
    public Guid ClientId { get; }
    public Guid AssumptionSetId { get; private set; }
    public int AssumptionSetVersion { get; private set; }
    public Guid CreatedBy { get; }
    public AnalysisStatus Status { get; private set; } = AnalysisStatus.Draft;

    /// <summary>Incremented every time a result is recorded.</summary>
    public int Version { get; private set; }

    /// <summary>JSON snapshot of the engine result for the current version.</summary>
    public string? ResultJson { get; private set; }

    /// <summary>SHA-256 of <see cref="ResultJson"/>; embedded in reports and the audit chain.</summary>
    public string? ResultHash { get; private set; }

    public DateTime? CalculatedAtUtc { get; private set; }
    public string? EngineVersion { get; private set; }

    /// <summary>Free-form overrides the adviser applied on top of the assumption set, captured for audit.</summary>
    public string? OverridesJson { get; private set; }

    public bool IsLocked => Status == AnalysisStatus.Locked;

    protected void EnsureEditable() => Guard.Against(IsLocked, "This analysis is locked; create a new version to change it.");

    public void SetAssumptions(Guid assumptionSetId, int assumptionSetVersion, string? overridesJson, DateTime nowUtc)
    {
        EnsureEditable();
        AssumptionSetId = Guard.NotEmpty(assumptionSetId);
        AssumptionSetVersion = Guard.Positive(assumptionSetVersion);
        OverridesJson = overridesJson;
        Invalidate(nowUtc);
    }

    /// <summary>Stores a calculation result, bumping the version and moving to Calculated.</summary>
    public void RecordResult(string resultJson, string resultHash, string engineVersion, DateTime nowUtc)
    {
        EnsureEditable();
        ResultJson = Guard.NotNullOrWhiteSpace(resultJson);
        ResultHash = Guard.NotNullOrWhiteSpace(resultHash);
        EngineVersion = Guard.NotNullOrWhiteSpace(engineVersion);
        CalculatedAtUtc = nowUtc;
        Version++;
        Status = AnalysisStatus.Calculated;
        Touch(nowUtc);
    }

    /// <summary>Marks the analysis as needing recalculation after inputs change.</summary>
    protected void Invalidate(DateTime nowUtc)
    {
        EnsureEditable();
        if (Status == AnalysisStatus.Calculated)
        {
            Status = AnalysisStatus.Draft;
        }

        Touch(nowUtc);
    }

    /// <summary>Freezes the analysis once a report has been issued from it.</summary>
    public void Lock(DateTime nowUtc)
    {
        Guard.Against(Status != AnalysisStatus.Calculated, "Only a calculated analysis can be locked.");
        Status = AnalysisStatus.Locked;
        Touch(nowUtc);
    }
}
