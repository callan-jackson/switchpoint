using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Clients;

/// <summary>Link back to the record in the back-office system the client was imported from.</summary>
public sealed record ExternalReference
{
    public ExternalReference(ExternalSource source, string? externalId)
    {
        Source = Guard.Defined(source);
        Guard.Against(source != ExternalSource.Manual && string.IsNullOrWhiteSpace(externalId), "An imported client must carry the source system identifier.");
        ExternalId = externalId;
    }

    public ExternalSource Source { get; }
    public string? ExternalId { get; }

    public static ExternalReference Manual { get; } = new(ExternalSource.Manual, null);
}
