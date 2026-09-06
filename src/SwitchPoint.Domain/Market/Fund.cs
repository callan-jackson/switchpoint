using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Domain.Market;

public enum FundType
{
    Oeic = 0,
    UnitTrust = 1,
    Etf = 2,
    InvestmentTrust = 3,
    ModelPortfolio = 4,
    Cash = 5,
    WithProfits = 6,
}

/// <summary>Performance and risk statistics as supplied by a fund data feed (Morningstar-style).</summary>
public sealed record FundStatistics(
    decimal? Return1Y,
    decimal? Return3YAnnualised,
    decimal? Return5YAnnualised,
    decimal? Volatility3Y,
    decimal? Sharpe3Y,
    decimal? MaxDrawdown3Y,
    decimal? Yield,
    int? MorningstarRating,
    string? MedalistRating);

/// <summary>A fund, ETF, investment trust or cash option in the research universe.</summary>
public sealed class Fund : Entity
{
    public Fund(
        Guid id,
        string isin,
        string name,
        string managerName,
        FundType type,
        decimal ocf,
        AssetAllocation assetAllocation,
        DateTime createdAtUtc,
        string? sedol = null,
        string? shareClass = null,
        string? iaSector = null,
        string? morningstarCategory = null,
        int? srri = null)
        : base(id, createdAtUtc)
    {
        Guard.Against(!Holding.IsValidIsin(isin), $"'{isin}' is not a valid ISIN.");
        Isin = isin.ToUpperInvariant();
        Name = Guard.NotNullOrWhiteSpace(name);
        ManagerName = Guard.NotNullOrWhiteSpace(managerName);
        Type = Guard.Defined(type);
        Ocf = Guard.InRange(ocf, 0m, 0.1m);
        AssetAllocation = Guard.NotNull(assetAllocation);
        Sedol = sedol;
        ShareClass = shareClass;
        IaSector = iaSector;
        MorningstarCategory = morningstarCategory;
        if (srri is { } s)
        {
            Guard.InRange(s, 1, 7, nameof(srri));
        }

        Srri = srri;
    }

    public string Isin { get; }
    public string? Sedol { get; private set; }
    public string Name { get; private set; }
    public string? ShareClass { get; private set; }
    public string ManagerName { get; private set; }
    public FundType Type { get; private set; }
    public string? IaSector { get; private set; }
    public string? MorningstarCategory { get; private set; }

    /// <summary>Ongoing charges figure as a fraction (0.0022m = 0.22%).</summary>
    public decimal Ocf { get; private set; }

    public decimal TransactionCosts { get; private set; }
    public AssetAllocation AssetAllocation { get; private set; }

    /// <summary>Synthetic risk and reward indicator, 1 (lowest) to 7.</summary>
    public int? Srri { get; private set; }

    public FundStatistics Statistics { get; private set; } = new(null, null, null, null, null, null, null, null, null);
    public decimal? Price { get; private set; }
    public DateOnly? PriceDate { get; private set; }
    public string? FactsheetUrl { get; private set; }
    public string? SourceUrl { get; private set; }
    public DateOnly? AsAt { get; private set; }

    public void UpdateCharges(decimal ocf, decimal transactionCosts, DateOnly asAt, string? sourceUrl, DateTime nowUtc)
    {
        Ocf = Guard.InRange(ocf, 0m, 0.1m);
        TransactionCosts = Guard.InRange(transactionCosts, 0m, 0.1m);
        AsAt = asAt;
        SourceUrl = sourceUrl;
        Touch(nowUtc);
    }

    public void UpdateStatistics(FundStatistics statistics, decimal? price, DateOnly? priceDate, DateTime nowUtc)
    {
        Statistics = Guard.NotNull(statistics);
        if (statistics.MorningstarRating is { } r)
        {
            Guard.InRange(r, 1, 5, nameof(statistics));
        }

        Price = price;
        PriceDate = priceDate;
        Touch(nowUtc);
    }

    public void UpdateProfile(string name, string managerName, string? shareClass, string? sedol, string? iaSector, string? morningstarCategory, int? srri, AssetAllocation allocation, string? factsheetUrl, DateTime nowUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        ManagerName = Guard.NotNullOrWhiteSpace(managerName);
        ShareClass = shareClass;
        Sedol = sedol;
        IaSector = iaSector;
        MorningstarCategory = morningstarCategory;
        if (srri is { } s)
        {
            Guard.InRange(s, 1, 7, nameof(srri));
        }

        Srri = srri;
        AssetAllocation = Guard.NotNull(allocation);
        FactsheetUrl = factsheetUrl;
        Touch(nowUtc);
    }
}
