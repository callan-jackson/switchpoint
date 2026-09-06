namespace SwitchPoint.Domain.Common;

/// <summary>
/// Thrown when a domain invariant or business rule is violated (for example modifying a locked
/// analysis, or constructing a tiered charge whose bands are not ascending). Argument-level
/// problems use the standard <see cref="ArgumentException"/> family instead.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
