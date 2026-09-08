using System.Diagnostics.CodeAnalysis;

namespace SwitchPoint.Domain.Common;

/// <summary>How often a payment (contribution or fixed charge) is made. <see cref="Single"/> is a one-off.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "A single premium is the industry term for a one-off payment; renaming it would cost more in clarity than the System.Single collision costs.")]
public enum Frequency
{
    Single = 0,
    Annually = 1,
    Quarterly = 4,
    Monthly = 12,
}
