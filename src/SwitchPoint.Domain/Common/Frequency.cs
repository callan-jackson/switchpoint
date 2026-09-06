namespace SwitchPoint.Domain.Common;

/// <summary>How often a payment (contribution or fixed charge) is made. <see cref="Single"/> is a one-off.</summary>
public enum Frequency
{
    Single = 0,
    Annually = 1,
    Quarterly = 4,
    Monthly = 12,
}
