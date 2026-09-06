using SwitchPoint.Domain.Audit;

namespace SwitchPoint.Domain.Tests.Audit;

public class HashChainTests
{
    private static readonly Guid Firm = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);

    private static List<AuditEvent> Chain(int n)
    {
        List<AuditEvent> events = [];
        string previous = AuditEvent.GenesisHash;
        for (int i = 0; i < n; i++)
        {
            AuditEvent e = new(Guid.NewGuid(), Firm, i, Guid.NewGuid(), T0.AddSeconds(i), "Client", Guid.NewGuid(), i % 2 == 0 ? "Created" : "Updated", $"{{\"n\":{i}}}", previous);
            events.Add(e);
            previous = e.Hash;
        }

        return events;
    }

    [Fact]
    public void Hash_is_lowercase_sha256_hex_and_deterministic()
    {
        string h1 = AuditEvent.ComputeHash(AuditEvent.GenesisHash, "abc");
        string h2 = AuditEvent.ComputeHash(AuditEvent.GenesisHash, "abc");
        Assert.Equal(64, h1.Length);
        Assert.Equal(h1, h2);
        Assert.Equal(h1, h1.ToLowerInvariant());
        Assert.NotEqual(h1, AuditEvent.ComputeHash(AuditEvent.GenesisHash, "abd"));
        Assert.NotEqual(h1, AuditEvent.ComputeHash(new string('1', 64), "abc"));
    }

    [Fact]
    public void Valid_chain_verifies()
    {
        ChainVerification v = HashChainVerifier.Verify(Chain(25));
        Assert.True(v.IsValid);
        Assert.Equal(-1, v.FirstBrokenIndex);
        Assert.Equal(25, v.EventsChecked);
    }

    [Fact]
    public void Empty_chain_is_valid() => Assert.True(HashChainVerifier.Verify([]).IsValid);

    [Fact]
    public void Tampered_payload_is_detected_at_that_index()
    {
        List<AuditEvent> chain = Chain(10);
        AuditEvent victim = chain[4];
        chain[4] = AuditEvent.Rehydrate(victim.Id, victim.FirmId, victim.Sequence, victim.UserId, victim.OccurredAtUtc, victim.EntityType, victim.EntityId, victim.Action, "{\"n\":999}", victim.PreviousHash, victim.Hash);
        ChainVerification v = HashChainVerifier.Verify(chain);
        Assert.False(v.IsValid);
        Assert.Equal(4, v.FirstBrokenIndex);
        Assert.Contains("tampered", v.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Removed_event_is_detected_by_sequence_gap()
    {
        List<AuditEvent> chain = Chain(10);
        chain.RemoveAt(3);
        ChainVerification v = HashChainVerifier.Verify(chain);
        Assert.False(v.IsValid);
        Assert.Equal(3, v.FirstBrokenIndex);
    }

    [Fact]
    public void Reordered_events_are_detected()
    {
        List<AuditEvent> chain = Chain(10);
        (chain[5], chain[6]) = (chain[6], chain[5]);
        Assert.Equal(5, HashChainVerifier.Verify(chain).FirstBrokenIndex);
    }

    [Fact]
    public void Rehydrated_intact_event_verifies()
    {
        AuditEvent e = Chain(1)[0];
        AuditEvent r = AuditEvent.Rehydrate(e.Id, e.FirmId, e.Sequence, e.UserId, e.OccurredAtUtc, e.EntityType, e.EntityId, e.Action, e.PayloadJson, e.PreviousHash, e.Hash);
        Assert.True(r.IsIntact());
        Assert.Equal(e.Hash, r.Hash);
    }

    [Fact]
    public void Foreign_firm_event_breaks_chain()
    {
        List<AuditEvent> chain = Chain(3);
        AuditEvent foreign = new(Guid.NewGuid(), Guid.NewGuid(), 3, null, T0, "Client", null, "Created", "{}", chain[2].Hash);
        chain.Add(foreign);
        Assert.Equal(3, HashChainVerifier.Verify(chain).FirstBrokenIndex);
    }

    [Fact]
    public void Previous_hash_must_be_64_chars() =>
        Assert.Throws<SwitchPoint.Domain.Common.DomainException>(() => new AuditEvent(Guid.NewGuid(), Firm, 0, null, T0, "Client", null, "Created", "{}", "abc"));
}
