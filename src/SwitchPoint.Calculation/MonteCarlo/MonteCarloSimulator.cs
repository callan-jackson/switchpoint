using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Calculation.MonteCarlo;

/// <summary>Inputs to a stochastic run. See docs/methodology/monte-carlo.md.</summary>
/// <param name="Deterministic">The cashflow plan; each path replaces asset growth (and optionally inflation) with sampled values.</param>
/// <param name="Assumptions">Capital market assumptions (expected return, volatility, correlations per asset class).</param>
/// <param name="Seed">Seed for xoshiro256**; identical seeds give identical paths on every platform.</param>
/// <param name="Paths">Number of simulated paths (100–10,000).</param>
/// <param name="InflationMean">Expected inflation when stochastic inflation is on.</param>
/// <param name="InflationVolatility">Inflation volatility; 0 keeps inflation fixed at the deterministic assumption.</param>
public sealed record MonteCarloRequest(CashflowRequest Deterministic, CapitalMarketAssumptions Assumptions, ulong Seed, int Paths = 1000, decimal InflationMean = 0.02m, decimal InflationVolatility = 0m);

/// <summary>Percentiles of a quantity across paths for one plan year.</summary>
public sealed record PercentileRow(int Year, int Age, decimal P5, decimal P10, decimal P25, decimal P50, decimal P75, decimal P90, decimal P95);

/// <summary>Check required by COBS 19.1.2CR: the 50th percentile must be no less conservative than the deterministic analysis.</summary>
public sealed record ConservativenessCheck(decimal DeterministicAssetsAtEnd, decimal MedianAssetsAtEnd, bool MedianIsNoLessConservative);

public sealed record MonteCarloResult(
    ulong Seed,
    int Paths,
    decimal ProbabilityOfSuccess,
    IReadOnlyList<PercentileRow> TotalAssetsReal,
    IReadOnlyList<PercentileRow> NetIncomeReal,
    int? MedianShortfallAge,
    int? WorstDecileShortfallAge,
    ConservativenessCheck Conservativeness,
    decimal MeanLegacyReal);

/// <summary>
/// Correlated lognormal asset-class returns (Cholesky) driving the deterministic cashflow engine path by path.
/// Random numbers are generated in double and converted to decimal at the boundary; all money arithmetic is decimal.
/// </summary>
public sealed class MonteCarloSimulator
{
    private static readonly AssetClass[] ClassOrder = [AssetClass.UkEquity, AssetClass.GlobalEquity, AssetClass.GovernmentBonds, AssetClass.CorporateBonds, AssetClass.Property, AssetClass.Cash, AssetClass.Alternatives];
    private readonly CashflowEngine _engine;

    public MonteCarloSimulator(CashflowEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public MonteCarloResult Run(MonteCarloRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Paths is < 100 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Paths, "Paths must be between 100 and 10,000.");
        }

        if (request.InflationVolatility < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.InflationVolatility, "Inflation volatility cannot be negative.");
        }

        CashflowRequest baseRequest = request.Deterministic;
        CashflowResult deterministic = _engine.Run(baseRequest);
        int years = deterministic.Rows.Count;

        // Per-class parameters in a fixed order; classes missing from the assumptions are treated as cash-like (0 vol, cash return).
        CapitalMarketAssumptions cma = request.Assumptions;
        int n = cma.AssetClasses.Count;
        double[] mu = new double[n];
        double[] sigma = new double[n];
        for (int i = 0; i < n; i++)
        {
            mu[i] = (double)cma.AssetClasses[i].ExpectedReturn;
            sigma[i] = (double)cma.AssetClasses[i].Volatility;
        }

        double[,] chol = Cholesky(cma.Correlations, n);
        Dictionary<AssetClass, int> index = [];
        for (int i = 0; i < n; i++)
        {
            index[cma.AssetClasses[i].AssetClass] = i;
        }

        // Per-asset class weights derived from the asset allocation (or a default by asset kind).
        Dictionary<string, double[]> weights = [];
        foreach (CashflowAsset a in baseRequest.Assets)
        {
            weights[a.Asset.Name] = WeightsFor(a, index, n);
        }

        Xoshiro256StarStar rng = new(request.Seed);
        decimal[][] assetsReal = new decimal[years][];
        decimal[][] incomeReal = new decimal[years][];
        for (int y = 0; y < years; y++)
        {
            assetsReal[y] = new decimal[request.Paths];
            incomeReal[y] = new decimal[request.Paths];
        }

        int successes = 0;
        List<int?> shortfallAges = [];
        decimal legacySum = 0m;
        double[] z = new double[n];
        double[] correlated = new double[n];

        for (int p = 0; p < request.Paths; p++)
        {
            // Pre-draw the path's returns so the engine callback is a pure lookup.
            decimal[][] classReturns = new decimal[years][];
            decimal[] inflation = new decimal[years];
            for (int y = 0; y < years; y++)
            {
                rng.FillStandardNormals(z);
                for (int i = 0; i < n; i++)
                {
                    double s = 0;
                    for (int j = 0; j <= i; j++)
                    {
                        s += chol[i, j] * z[j];
                    }

                    correlated[i] = s;
                }

                classReturns[y] = new decimal[n];
                for (int i = 0; i < n; i++)
                {
                    // Lognormal: exp(μ − σ²/2 + σ·ε) − 1, so the arithmetic mean return is ≈ μ.
                    double lnReturn = Math.Log(1 + mu[i]) - (sigma[i] * sigma[i] / 2) + (sigma[i] * correlated[i]);
                    classReturns[y][i] = ToDecimal(Math.Exp(lnReturn) - 1);
                }

                inflation[y] = request.InflationVolatility == 0m
                    ? baseRequest.Inflation
                    : ToDecimal((double)request.InflationMean + ((double)request.InflationVolatility * rng.NextStandardNormal()));
            }

            CashflowRequest pathRequest = baseRequest with
            {
                ReturnOverride = (year, asset) =>
                {
                    // Assets created by the engine at run time (e.g. surplus ISA, cash) are weighted by kind.
                    if (!weights.TryGetValue(asset.Asset.Name, out double[]? w))
                    {
                        w = WeightsFor(asset, index, n);
                        weights[asset.Asset.Name] = w;
                    }

                    decimal ret = 0m;
                    for (int i = 0; i < n; i++)
                    {
                        if (w[i] != 0)
                        {
                            ret += ToDecimal(w[i]) * classReturns[Math.Min(year, years - 1)][i];
                        }
                    }

                    return ret;
                },
                InflationOverride = request.InflationVolatility == 0m ? null : year => inflation[Math.Min(year, years - 1)],
            };

            CashflowResult result = _engine.Run(pathRequest);
            if (result.Succeeds)
            {
                successes++;
            }

            shortfallAges.Add(result.FirstShortfallAge);
            legacySum += result.LegacyAtEndReal;
            for (int y = 0; y < years; y++)
            {
                assetsReal[y][p] = result.Rows[y].TotalAssetsReal;
                incomeReal[y][p] = result.Rows[y].NetIncomeReal;
            }
        }

        List<PercentileRow> assetRows = [];
        List<PercentileRow> incomeRows = [];
        for (int y = 0; y < years; y++)
        {
            Array.Sort(assetsReal[y]);
            Array.Sort(incomeReal[y]);
            assetRows.Add(Percentiles(deterministic.Rows[y].Year, deterministic.Rows[y].Age, assetsReal[y]));
            incomeRows.Add(Percentiles(deterministic.Rows[y].Year, deterministic.Rows[y].Age, incomeReal[y]));
        }

        int?[] sortedShortfalls = [.. shortfallAges.OrderBy(a => a ?? int.MaxValue)];
        int? medianShortfall = sortedShortfalls[sortedShortfalls.Length / 2];
        int? worstDecile = sortedShortfalls[Math.Max(0, (sortedShortfalls.Length / 10) - 1)];
        decimal medianEnd = assetRows[^1].P50;
        ConservativenessCheck check = new(deterministic.LegacyAtEndReal, medianEnd, medianEnd <= deterministic.LegacyAtEndReal + 0.005m);

        return new MonteCarloResult(request.Seed, request.Paths, (decimal)successes / request.Paths, assetRows, incomeRows, medianShortfall, worstDecile, check, legacySum / request.Paths);
    }

    /// <summary>Lower-triangular Cholesky factor of a correlation matrix (double precision; used only to shape random draws).</summary>
    internal static double[,] Cholesky(decimal[,] correlations, int n)
    {
        double[,] l = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = (double)correlations[i, j];
                for (int k = 0; k < j; k++)
                {
                    sum -= l[i, k] * l[j, k];
                }

                if (i == j)
                {
                    if (sum <= 0)
                    {
                        throw new ArgumentException("The correlation matrix is not positive definite.", nameof(correlations));
                    }

                    l[i, j] = Math.Sqrt(sum);
                }
                else
                {
                    l[i, j] = sum / l[j, j];
                }
            }
        }

        return l;
    }

    private static double[] WeightsFor(CashflowAsset a, Dictionary<AssetClass, int> index, int n)
    {
        double[] w = new double[n];
        void Put(AssetClass c, decimal weight)
        {
            if (weight != 0m && index.TryGetValue(c, out int i))
            {
                w[i] += (double)weight;
            }
            else if (weight != 0m && index.TryGetValue(AssetClass.Cash, out int cashIndex))
            {
                w[cashIndex] += (double)weight;
            }
        }

        if (a.Allocation is { } alloc)
        {
            Put(AssetClass.GlobalEquity, alloc.Equity);
            Put(AssetClass.GovernmentBonds, alloc.FixedInterest);
            Put(AssetClass.Property, alloc.Property);
            Put(AssetClass.Cash, alloc.Cash);
            Put(AssetClass.Alternatives, alloc.Alternatives);
            return w;
        }

        switch (a.Asset.Kind)
        {
            case Domain.Analysis.PlanAssetKind.Cash:
                Put(AssetClass.Cash, 1m);
                break;
            case Domain.Analysis.PlanAssetKind.Property:
                Put(AssetClass.Property, 1m);
                break;
            default:
                Put(AssetClass.GlobalEquity, 0.6m);
                Put(AssetClass.GovernmentBonds, 0.4m);
                break;
        }

        return w;
    }

    private static PercentileRow Percentiles(int year, int age, decimal[] sorted) => new(
        year, age, Quantile(sorted, 0.05), Quantile(sorted, 0.10), Quantile(sorted, 0.25), Quantile(sorted, 0.50), Quantile(sorted, 0.75), Quantile(sorted, 0.90), Quantile(sorted, 0.95));

    private static decimal Quantile(decimal[] sorted, double q)
    {
        if (sorted.Length == 0)
        {
            return 0m;
        }

        double position = q * (sorted.Length - 1);
        int lo = (int)Math.Floor(position);
        int hi = Math.Min(sorted.Length - 1, lo + 1);
        decimal frac = ToDecimal(position - lo);
        return sorted[lo] + ((sorted[hi] - sorted[lo]) * frac);
    }

    private static decimal ToDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArithmeticException("A non-finite value was produced by the random return model.");
        }

        double clamped = Math.Clamp(value, -0.99, 50);
        return Math.Round((decimal)clamped, 12, MidpointRounding.ToEven);
    }
}
