using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Infrastructure.Persistence.Json;

/// <summary>Stores a value as canonical JSON text in a column.</summary>
public sealed class JsonValueConverter<T> : ValueConverter<T, string>
{
    public JsonValueConverter()
        : base(v => JsonDefaults.Serialize(v), s => JsonDefaults.Deserialize<T>(s)!)
    {
    }

    /// <summary>Change-tracking comparer over the canonical JSON.</summary>
    public ValueComparer<T> Comparer { get; } = new(
        (a, b) => JsonDefaults.Serialize(a) == JsonDefaults.Serialize(b),
        v => JsonDefaults.Serialize(v).GetHashCode(StringComparison.Ordinal),
        v => JsonDefaults.Deserialize<T>(JsonDefaults.Serialize(v))!);
}

/// <summary>Stores a domain value through an Application DTO so private-constructor records round-trip safely.</summary>
public sealed class MappedJsonValueConverter<TDomain, TDto> : ValueConverter<TDomain, string>
{
    public MappedJsonValueConverter(Func<TDomain, TDto> toDto, Func<TDto, TDomain> toDomain)
        : base(v => JsonDefaults.Serialize(toDto(v)), s => toDomain(JsonDefaults.Deserialize<TDto>(s)!))
    {
        Comparer = new ValueComparer<TDomain>(
            (a, b) => JsonDefaults.Serialize(toDto(a!)) == JsonDefaults.Serialize(toDto(b!)),
            v => JsonDefaults.Serialize(toDto(v)).GetHashCode(StringComparison.Ordinal),
            v => toDomain(JsonDefaults.Deserialize<TDto>(JsonDefaults.Serialize(toDto(v)))!));
    }

    /// <summary>Change-tracking comparer that goes through the DTO (safe for private-constructor records).</summary>
    public ValueComparer<TDomain> Comparer { get; }
}

/// <summary>Persistence shape of a plan asset (its charge schedule needs the DTO form).</summary>
public sealed record PlanAssetJson(string Name, PlanAssetKind Kind, decimal Value, decimal GrowthRate, ChargeScheduleDto Charges, Guid? SchemeId, decimal CostBasis, decimal AnnualContribution, decimal EmployerContribution, bool SalarySacrifice);

/// <summary>Persistence shape of capital market assumptions (multi-dimensional arrays are not JSON-serialisable).</summary>
public sealed record CapitalMarketAssumptionsJson(IReadOnlyList<AssetClassAssumption> AssetClasses, IReadOnlyList<IReadOnlyList<decimal>> Correlations, DateOnly AsAt, string Source);

/// <summary>Factory for the converters used by the model configuration.</summary>
public static class Converters
{
    public static MappedJsonValueConverter<ChargeSchedule, ChargeScheduleDto> ChargeSchedule { get; } = new(c => c.ToDto(), d => d.ToDomain());

    public static MappedJsonValueConverter<Guarantees, GuaranteesDto> Guarantees { get; } = new(g => g.ToDto(), d => d.ToDomain());

    public static MappedJsonValueConverter<AdviserCharge, AdviserChargeDto> AdviserCharge { get; } = new(a => a.ToDto(), d => d.ToDomain());

    public static MappedJsonValueConverter<List<Holding>, List<HoldingDto>> Holdings { get; } = new(
        h => h.Select(x => x.ToDto()).ToList(),
        d => d.Select(x => x.ToDomain()).ToList());

    public static MappedJsonValueConverter<List<Contribution>, List<ContributionDto>> Contributions { get; } = new(
        c => c.Select(x => x.ToDto()).ToList(),
        d => d.Select(x => x.ToDomain()).ToList());

    public static MappedJsonValueConverter<List<DbTranche>, List<DbTrancheDto>> Tranches { get; } = new(
        t => t.Select(x => new DbTrancheDto(x.Name, x.AccruedAnnualPension, x.Revaluation.ToDto(), x.Escalation.ToDto(), x.IsGmp)).ToList(),
        d => d.Select(x => new DbTranche(x.Name, x.AccruedAnnualPension, x.Revaluation.ToRevaluation(), x.Escalation.ToEscalation(), x.IsGmp)).ToList());

    public static MappedJsonValueConverter<List<ProductChargeVersion>, List<ChargeVersionDto>> ChargeVersions { get; } = new(
        v => v.Select(x => new ChargeVersionDto(x.Version, x.EffectiveFrom, x.EffectiveTo, x.AsAt, x.SourceUrl, x.DataQuality, x.Charges.ToDto())).ToList(),
        d => d.Select(x => new ProductChargeVersion(x.Version, x.Charges.ToDomain(), x.EffectiveFrom, x.EffectiveTo, x.SourceUrl, x.AsAt, x.DataQuality)).ToList());

    public static MappedJsonValueConverter<List<PlanAsset>, List<PlanAssetJson>> PlanAssets { get; } = new(
        a => a.Select(x => new PlanAssetJson(x.Name, x.Kind, x.Value, x.GrowthRate, x.Charges.ToDto(), x.SchemeId, x.CostBasis, x.AnnualContribution, x.EmployerContribution, x.SalarySacrifice)).ToList(),
        d => d.Select(x => new PlanAsset(x.Name, x.Kind, x.Value, x.GrowthRate, x.Charges.ToDomain(), x.SchemeId, x.CostBasis, x.AnnualContribution, x.EmployerContribution, x.SalarySacrifice)).ToList());

    public static MappedJsonValueConverter<CapitalMarketAssumptions?, CapitalMarketAssumptionsJson?> CapitalMarketAssumptions { get; } = new(
        c => c is null ? null : new CapitalMarketAssumptionsJson(c.AssetClasses, ToJagged(c.Correlations), c.AsAt, c.Source),
        j => j is null ? null : new CapitalMarketAssumptions(j.AssetClasses, ToMatrix(j.Correlations), j.AsAt, j.Source));

    public static JsonValueConverter<List<Guid>> GuidList { get; } = new();

    public static JsonValueConverter<List<PlanIncome>> PlanIncomes { get; } = new();

    public static JsonValueConverter<List<PlanExpensePhase>> PlanExpenses { get; } = new();

    public static JsonValueConverter<List<PlanEvent>> PlanEvents { get; } = new();

    public static JsonValueConverter<PlanStrategy> PlanStrategy { get; } = new();

    public static JsonValueConverter<List<ModelPortfolioHolding>> ModelPortfolioHoldings { get; } = new();

    public static JsonValueConverter<MarketInputs> MarketInputs { get; } = new();

    public static JsonValueConverter<AssetAllocation> AssetAllocation { get; } = new();

    public static JsonValueConverter<FundStatistics> FundStatistics { get; } = new();

    public static JsonValueConverter<Domain.Clients.Address?> Address { get; } = new();

    public static JsonValueConverter<Domain.Clients.StatePensionForecast> StatePension { get; } = new();

    public static JsonValueConverter<Domain.Clients.ExternalReference> ExternalReference { get; } = new();

    private static IReadOnlyList<IReadOnlyList<decimal>> ToJagged(decimal[,] m)
    {
        int n = m.GetLength(0);
        List<IReadOnlyList<decimal>> rows = [];
        for (int i = 0; i < n; i++)
        {
            List<decimal> row = [];
            for (int j = 0; j < m.GetLength(1); j++)
            {
                row.Add(m[i, j]);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static decimal[,] ToMatrix(IReadOnlyList<IReadOnlyList<decimal>> rows)
    {
        int n = rows.Count;
        decimal[,] m = new decimal[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                m[i, j] = rows[i][j];
            }
        }

        return m;
    }
}
