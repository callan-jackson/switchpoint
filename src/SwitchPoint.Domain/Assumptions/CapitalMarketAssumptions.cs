using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Assumptions;

/// <summary>Expected nominal return and volatility for one asset class.</summary>
public sealed record AssetClassAssumption
{
    public AssetClassAssumption(AssetClass assetClass, decimal expectedReturn, decimal volatility)
    {
        AssetClass = Guard.Defined(assetClass);
        ExpectedReturn = Guard.InRange(expectedReturn, -0.2m, 0.3m);
        Volatility = Guard.InRange(volatility, 0m, 1m);
    }

    public AssetClass AssetClass { get; }
    public decimal ExpectedReturn { get; }
    public decimal Volatility { get; }
}

/// <summary>Capital market assumptions: per-class return/volatility plus a correlation matrix in enum order.</summary>
public sealed record CapitalMarketAssumptions
{
    public CapitalMarketAssumptions(IEnumerable<AssetClassAssumption> assetClasses, decimal[,] correlations, DateOnly asAt, string source)
    {
        AssetClasses = Guard.NotEmpty(assetClasses);
        int n = AssetClasses.Count;
        Guard.Against(AssetClasses.Select(a => a.AssetClass).Distinct().Count() != n, "Each asset class may appear only once.");
        Guard.NotNull(correlations);
        Guard.Against(correlations.GetLength(0) != n || correlations.GetLength(1) != n, $"The correlation matrix must be {n}×{n}.");
        for (int i = 0; i < n; i++)
        {
            Guard.Against(correlations[i, i] != 1m, "Correlation matrix diagonal must be 1.");
            for (int j = 0; j < n; j++)
            {
                Guard.Against(correlations[i, j] < -1m || correlations[i, j] > 1m, "Correlations must lie in [-1, 1].");
                Guard.Against(correlations[i, j] != correlations[j, i], "The correlation matrix must be symmetric.");
            }
        }

        Correlations = (decimal[,])correlations.Clone();
        AsAt = asAt;
        Source = Guard.NotNullOrWhiteSpace(source);
    }

    public IReadOnlyList<AssetClassAssumption> AssetClasses { get; }
    public decimal[,] Correlations { get; }
    public DateOnly AsAt { get; }
    public string Source { get; }

    public AssetClassAssumption For(AssetClass assetClass) =>
        AssetClasses.FirstOrDefault(a => a.AssetClass == assetClass) ?? throw new DomainException($"No assumption for asset class {assetClass}.");

    public bool Equals(CapitalMarketAssumptions? other)
    {
        if (other is null || AsAt != other.AsAt || Source != other.Source || !AssetClasses.SequenceEqual(other.AssetClasses))
        {
            return false;
        }

        return Correlations.Cast<decimal>().SequenceEqual(other.Correlations.Cast<decimal>());
    }

    public override int GetHashCode() => HashCode.Combine(AsAt, Source, AssetClasses.Count);
}
