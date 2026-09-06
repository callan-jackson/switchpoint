using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>An investment held within an arrangement, identified by ISIN and/or catalogue fund id.</summary>
public sealed record Holding
{
    public Holding(string name, decimal weight, string? isin = null, Guid? fundId = null, decimal? ocf = null)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Weight = Guard.Fraction(weight);
        Guard.Against(isin is not null && !IsValidIsin(isin), $"'{isin}' is not a valid ISIN.");
        Isin = isin?.ToUpperInvariant();
        FundId = fundId;
        if (ocf is { } o)
        {
            Guard.Fraction(o, nameof(ocf));
        }

        Ocf = ocf;
    }

    public string Name { get; }
    public decimal Weight { get; }
    public string? Isin { get; }
    public Guid? FundId { get; }

    /// <summary>Ongoing charges figure known at the time of entry (overrides the catalogue value when set).</summary>
    public decimal? Ocf { get; }

    /// <summary>ISIN format check: two letters, nine alphanumerics, one check digit, with a valid Luhn checksum.</summary>
    public static bool IsValidIsin(string isin)
    {
        if (string.IsNullOrWhiteSpace(isin) || isin.Length != 12)
        {
            return false;
        }

        string s = isin.ToUpperInvariant();
        if (!char.IsLetter(s[0]) || !char.IsLetter(s[1]) || !s.All(char.IsLetterOrDigit) || !char.IsDigit(s[11]))
        {
            return false;
        }

        // Expand letters to digits (A=10 ... Z=35) then apply Luhn.
        System.Text.StringBuilder digits = new();
        foreach (char c in s)
        {
            digits.Append(char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        int sum = 0;
        bool doubleIt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    /// <summary>Weighted OCF across holdings, using the holding's own OCF or the supplied catalogue lookup.</summary>
    public static decimal WeightedOcf(IEnumerable<Holding> holdings, Func<Holding, decimal?>? catalogueOcf = null)
    {
        decimal total = 0m;
        decimal weights = 0m;
        foreach (Holding h in holdings)
        {
            decimal? ocf = h.Ocf ?? catalogueOcf?.Invoke(h);
            Guard.Against(ocf is null, $"No OCF is known for holding '{h.Name}'.");
            total += h.Weight * ocf!.Value;
            weights += h.Weight;
        }

        return weights == 0m ? 0m : total / weights;
    }
}
