namespace SwitchPoint.Domain.Common;

/// <summary>An aggregate that belongs to exactly one firm; repositories filter on <see cref="FirmId"/>.</summary>
public interface ITenantScoped
{
    Guid FirmId { get; }
}
