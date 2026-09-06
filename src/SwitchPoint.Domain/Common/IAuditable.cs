namespace SwitchPoint.Domain.Common;

/// <summary>An object that records when it was created and last changed (UTC).</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }

    DateTime UpdatedAtUtc { get; }
}
