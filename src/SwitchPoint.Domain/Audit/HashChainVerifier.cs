namespace SwitchPoint.Domain.Audit;

/// <summary>Result of walking an audit chain.</summary>
/// <param name="IsValid">True when every link is intact and correctly chained.</param>
/// <param name="FirstBrokenIndex">Index (in the supplied order) of the first bad event, or -1.</param>
/// <param name="Reason">Human-readable description of the first failure, if any.</param>
/// <param name="EventsChecked">How many events were examined before stopping.</param>
public sealed record ChainVerification(bool IsValid, int FirstBrokenIndex, string? Reason, int EventsChecked)
{
    public static ChainVerification Valid(int count) => new(true, -1, null, count);
}

/// <summary>Walks an ordered sequence of audit events and reports the first broken link.</summary>
public static class HashChainVerifier
{
    public static ChainVerification Verify(IEnumerable<AuditEvent> orderedEvents)
    {
        ArgumentNullException.ThrowIfNull(orderedEvents);
        string expectedPrevious = AuditEvent.GenesisHash;
        long expectedSequence = 0;
        int index = 0;
        Guid? firm = null;
        foreach (AuditEvent e in orderedEvents)
        {
            firm ??= e.FirmId;
            if (e.FirmId != firm)
            {
                return new ChainVerification(false, index, $"Event {e.Id} belongs to a different firm.", index + 1);
            }

            if (e.Sequence != expectedSequence)
            {
                return new ChainVerification(false, index, $"Expected sequence {expectedSequence} but found {e.Sequence} (a link is missing or reordered).", index + 1);
            }

            if (!string.Equals(e.PreviousHash, expectedPrevious, StringComparison.Ordinal))
            {
                return new ChainVerification(false, index, $"Event {e.Id} does not chain to its predecessor.", index + 1);
            }

            if (!e.IsIntact())
            {
                return new ChainVerification(false, index, $"Event {e.Id} content does not match its hash (tampered).", index + 1);
            }

            expectedPrevious = e.Hash;
            expectedSequence++;
            index++;
        }

        return ChainVerification.Valid(index);
    }
}
