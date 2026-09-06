namespace SwitchPoint.Domain.Common;

/// <summary>
/// Base class for objects with identity. Two entities are equal when they are the same runtime type
/// and share an <see cref="Id"/>. The domain has no clock, so callers supply timestamps.
/// </summary>
public abstract class Entity : IAuditable
{
    protected Entity(Guid id, DateTime createdAtUtc)
    {
        Id = Guard.NotEmpty(id);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Records that the entity changed; intended for the persistence layer and mutating domain methods.</summary>
    public void MarkUpdated(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    protected void Touch(DateTime nowUtc) => MarkUpdated(nowUtc);

    public override bool Equals(object? obj)
        => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
