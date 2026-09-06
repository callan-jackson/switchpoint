using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Tenancy;

/// <summary>An adviser firm: the tenancy boundary for every client, analysis and audit chain.</summary>
public sealed class Firm : Entity
{
    public Firm(Guid id, string name, string fcaFirmReferenceNumber, DateTime createdAtUtc, Guid? defaultAssumptionSetId = null)
        : base(id, createdAtUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        FcaFirmReferenceNumber = Guard.NotNullOrWhiteSpace(fcaFirmReferenceNumber);
        DefaultAssumptionSetId = defaultAssumptionSetId;
    }

    public string Name { get; private set; }

    /// <summary>The firm's FCA Firm Reference Number (FRN), shown on every report.</summary>
    public string FcaFirmReferenceNumber { get; private set; }

    public Guid? DefaultAssumptionSetId { get; private set; }

    public void Rename(string name, DateTime nowUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Touch(nowUtc);
    }

    public void SetDefaultAssumptionSet(Guid assumptionSetId, DateTime nowUtc)
    {
        DefaultAssumptionSetId = Guard.NotEmpty(assumptionSetId);
        Touch(nowUtc);
    }
}
