using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Market;

public enum ProviderKind
{
    Platform = 0,
    Insurer = 1,
    SippOperator = 2,
    FundManager = 3,
    DiscretionaryManager = 4,
}

/// <summary>A pension provider, platform, insurer or fund manager in the market catalogue.</summary>
public sealed class Provider : Entity
{
    public Provider(Guid id, string name, ProviderKind kind, DateTime createdAtUtc, string? fcaFirmReferenceNumber = null, string? website = null)
        : base(id, createdAtUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Kind = Guard.Defined(kind);
        FcaFirmReferenceNumber = fcaFirmReferenceNumber;
        Website = website;
    }

    public string Name { get; private set; }
    public ProviderKind Kind { get; private set; }
    public string? FcaFirmReferenceNumber { get; private set; }
    public string? Website { get; private set; }

    public void Update(string name, ProviderKind kind, string? fcaFirmReferenceNumber, string? website, DateTime nowUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Kind = Guard.Defined(kind);
        FcaFirmReferenceNumber = fcaFirmReferenceNumber;
        Website = website;
        Touch(nowUtc);
    }
}
