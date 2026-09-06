using System.Security.Cryptography;
using System.Text;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Audit;

/// <summary>
/// One immutable link in a firm's tamper-evident audit chain. The hash covers the previous hash and
/// the canonical JSON of this event's content, so altering or removing any event breaks every later link.
/// See ADR-0005.
/// </summary>
public sealed class AuditEvent : ITenantScoped
{
    /// <summary>Hash used as the predecessor of the first event in a chain (64 zeros).</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    public AuditEvent(Guid id, Guid firmId, long sequence, Guid? userId, DateTime occurredAtUtc, string entityType, Guid? entityId, string action, string payloadJson, string previousHash)
    {
        Id = Guard.NotEmpty(id);
        FirmId = Guard.NotEmpty(firmId);
        Guard.Against(sequence < 0, "Sequence must be non-negative.");
        Sequence = sequence;
        UserId = userId;
        OccurredAtUtc = occurredAtUtc;
        EntityType = Guard.NotNullOrWhiteSpace(entityType);
        EntityId = entityId;
        Action = Guard.NotNullOrWhiteSpace(action);
        PayloadJson = Guard.NotNull(payloadJson);
        PreviousHash = Guard.NotNullOrWhiteSpace(previousHash);
        Guard.Against(previousHash.Length != 64, "PreviousHash must be a 64-character SHA-256 hex digest.");
        Hash = ComputeHash(previousHash, CanonicalContent());
    }

    public Guid Id { get; }
    public Guid FirmId { get; }

    /// <summary>Position in the firm's chain, starting at 0.</summary>
    public long Sequence { get; }

    public Guid? UserId { get; }
    public DateTime OccurredAtUtc { get; }
    public string EntityType { get; }
    public Guid? EntityId { get; }
    public string Action { get; }
    public string PayloadJson { get; }
    public string PreviousHash { get; }
    public string Hash { get; }

    /// <summary>Deterministic content string hashed into the chain (a pipe-delimited canonical form independent of JSON serialiser settings).</summary>
    public string CanonicalContent() => string.Join('|',
        Id.ToString("D"),
        FirmId.ToString("D"),
        Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
        UserId?.ToString("D") ?? string.Empty,
        OccurredAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        EntityType,
        EntityId?.ToString("D") ?? string.Empty,
        Action,
        PayloadJson);

    /// <summary>Recomputes the hash from stored fields; true when it matches <see cref="Hash"/>.</summary>
    public bool IsIntact() => string.Equals(Hash, ComputeHash(PreviousHash, CanonicalContent()), StringComparison.Ordinal);

    /// <summary>SHA-256 over previousHash + '\n' + canonical content, lower-case hex.</summary>
    public static string ComputeHash(string previousHash, string canonicalContent)
    {
        Guard.NotNull(previousHash);
        Guard.NotNull(canonicalContent);
        byte[] bytes = Encoding.UTF8.GetBytes(previousHash + "\n" + canonicalContent);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Rehydrates an event from storage, keeping the stored hash so that tampering can be detected by <see cref="IsIntact"/>.</summary>
    public static AuditEvent Rehydrate(Guid id, Guid firmId, long sequence, Guid? userId, DateTime occurredAtUtc, string entityType, Guid? entityId, string action, string payloadJson, string previousHash, string storedHash)
    {
        AuditEvent fresh = new(id, firmId, sequence, userId, occurredAtUtc, entityType, entityId, action, payloadJson, previousHash);
        return string.Equals(fresh.Hash, storedHash, StringComparison.Ordinal) ? fresh : new AuditEvent(fresh, storedHash);
    }

    private AuditEvent(AuditEvent source, string storedHash)
    {
        Id = source.Id;
        FirmId = source.FirmId;
        Sequence = source.Sequence;
        UserId = source.UserId;
        OccurredAtUtc = source.OccurredAtUtc;
        EntityType = source.EntityType;
        EntityId = source.EntityId;
        Action = source.Action;
        PayloadJson = source.PayloadJson;
        PreviousHash = source.PreviousHash;
        Hash = storedHash;
    }
}
