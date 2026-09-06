namespace SwitchPoint.Domain.Common;

/// <summary>Helpers for <see cref="Frequency"/>.</summary>
public static class FrequencyExtensions
{
    /// <summary>Number of payments in a year; zero for a one-off.</summary>
    public static int PaymentsPerYear(this Frequency frequency) => (int)Guard.Defined(frequency);

    /// <summary>Annualises a per-payment amount. A one-off is returned unchanged (it is a first-year amount).</summary>
    public static decimal Annualise(this Frequency frequency, decimal amountPerPayment)
        => frequency == Frequency.Single ? amountPerPayment : amountPerPayment * frequency.PaymentsPerYear();
}
