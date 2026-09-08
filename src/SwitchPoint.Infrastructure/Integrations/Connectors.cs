using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Infrastructure.Integrations;

/// <summary>Configuration for one integration (section Integrations:{Name}).</summary>
public sealed class IntegrationOptions
{
    public IntegrationMode Mode { get; set; } = IntegrationMode.Disabled;
    public string? BaseUrl { get; set; }
    public string? TokenUrl { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ApiKey { get; set; }
    public string? TenantId { get; set; }
}

/// <summary>All integration settings.</summary>
public sealed class IntegrationsOptions
{
    public IntegrationOptions Intelliflo { get; set; } = new();
    public IntegrationOptions Xplan { get; set; } = new();
    public IntegrationOptions TruePotential { get; set; } = new();
    public IntegrationOptions OrigoHub { get; set; } = new();
    public IntegrationOptions Morningstar { get; set; } = new();
}

/// <summary>Sandbox fixtures shared by the CRM connectors.</summary>
internal static class SandboxFixtures
{
    public static IReadOnlyList<ImportedClient> Clients(string source) =>
    [
        new(new ClientWrite { Title = "Mr", FirstName = "James", LastName = "Whitfield", DateOfBirth = new DateOnly(1968, 11, 2), Sex = Sex.Male, Email = "james.whitfield@example.com", MaritalStatus = MaritalStatus.Married, EmploymentStatus = EmploymentStatus.Employed, AnnualSalary = 61_000m, TargetRetirementAge = 66, RiskProfile = 4, StatePension = new StatePensionDto(241.30m, 35) },
            $"{source}-10001",
            [
                new SchemeWrite { Type = SchemeType.PersonalPension, ProductName = "Standard Life Personal Pension", PolicyNumber = $"{source}-PP-10001", CurrentValue = 88_400m, TransferValue = 88_400m, ValuationDate = new DateOnly(2026, 8, 31), Charges = new ChargeScheduleDto { ProductCharge = new TieredChargeDto(TieredChargeMode.WholeOfFund, [new TierBandDto(null, 0.95m)]), FundCharge = new FundChargeDto(FundChargeBasisKind.Explicit, 0.45m) }, Contributions = [new ContributionDto(ContributionPayer.Member, 200m, Frequency.Monthly, 0m, true, null, null)] },
                new SchemeWrite { Type = SchemeType.Isa, ProductName = "Stocks & Shares ISA", PolicyNumber = $"{source}-ISA-10001", CurrentValue = 32_000m, TransferValue = 32_000m, ValuationDate = new DateOnly(2026, 8, 31) },
            ]),
        new(new ClientWrite { Title = "Mrs", FirstName = "Helen", LastName = "Barrow", DateOfBirth = new DateOnly(1975, 4, 19), Sex = Sex.Female, Email = "helen.barrow@example.com", MaritalStatus = MaritalStatus.Divorced, EmploymentStatus = EmploymentStatus.SelfEmployed, AnnualSalary = 47_500m, TargetRetirementAge = 67, RiskProfile = 5, StatePension = new StatePensionDto(null, 28) },
            $"{source}-10002",
            [
                new SchemeWrite { Type = SchemeType.Sipp, ProductName = "Transact SIPP", PolicyNumber = $"{source}-SIPP-10002", CurrentValue = 154_000m, TransferValue = 154_000m, ValuationDate = new DateOnly(2026, 8, 31), Charges = new ChargeScheduleDto { PlatformCharge = new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(60_000m, 0.31m), new TierBandDto(300_000m, 0.28m), new TierBandDto(null, 0.20m)]), FundCharge = new FundChargeDto(FundChargeBasisKind.FromHoldings, null) }, Holdings = [new HoldingDto("Vanguard LifeStrategy 60% Equity", 100m, "GB00B3TYHH97", null, 0.22m)] },
            ]),
    ];
}

/// <summary>Base class: Disabled/Sandbox behaviour with a Live hook.</summary>
public abstract class BackOfficeConnectorBase(string name, IntegrationOptions options, ILogger logger) : IBackOfficeConnector
{
    public string Name { get; } = name;
    public IntegrationMode Mode => Options.Mode;
    public DateTime? LastSyncUtc { get; protected set; }
    protected IntegrationOptions Options { get; } = options;
    protected ILogger Logger { get; } = logger;

    public async Task<IReadOnlyList<ImportedClient>> ImportClientsAsync(string? externalClientId, CancellationToken ct = default)
    {
        IReadOnlyList<ImportedClient> result = Mode switch
        {
            IntegrationMode.Disabled => [],
            IntegrationMode.Sandbox => SandboxFixtures.Clients(Name),
            IntegrationMode.Live => await ImportLiveAsync(externalClientId, ct),
            _ => [],
        };
        if (externalClientId is not null)
        {
            result = [.. result.Where(r => string.Equals(r.ExternalId, externalClientId, StringComparison.OrdinalIgnoreCase))];
        }

        LastSyncUtc = DateTime.UtcNow;
        return result;
    }

    protected abstract Task<IReadOnlyList<ImportedClient>> ImportLiveAsync(string? externalClientId, CancellationToken ct);
}

/// <summary>
/// Intelliflo Office (intelliflo office) public API: OAuth2 client credentials at identity.gb.intelliflo.net/core/connect/token,
/// x-api-key header, GET /v2/clients and /v2/clients/{id}/plans (docs/research/market-research-brief.md §4).
/// </summary>
public sealed class IntellifloConnector(IHttpClientFactory httpClientFactory, IOptions<IntegrationsOptions> options, ILogger<IntellifloConnector> logger)
    : BackOfficeConnectorBase("Intelliflo", options.Value.Intelliflo, logger)
{
    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ClientsResponse(List<IoClient> Items);

    private sealed record IoClient(string Id, string? Title, string FirstName, string LastName, DateOnly DateOfBirth, string? Gender, string? Email);

    private sealed record PlansResponse(List<IoPlan> Items);

    private sealed record IoPlan(string Id, string? PlanType, string? Provider, string? ProductName, string? PolicyNumber, decimal? CurrentValue, decimal? TransferValue, DateOnly? ValuationDate);

    protected override async Task<IReadOnlyList<ImportedClient>> ImportLiveAsync(string? externalClientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Options.BaseUrl) || string.IsNullOrWhiteSpace(Options.ClientId) || string.IsNullOrWhiteSpace(Options.ClientSecret) || string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            Logger.LogWarning("Intelliflo is set to Live but is missing BaseUrl, ClientId, ClientSecret or ApiKey.");
            return [];
        }

        HttpClient http = httpClientFactory.CreateClient("Intelliflo");
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["scope"] = "client_data client_financial_data",
            ["tenant_id"] = Options.TenantId ?? string.Empty,
        });
        using HttpResponseMessage tokenResponse = await http.PostAsync(new Uri(Options.TokenUrl ?? "https://identity.gb.intelliflo.net/core/connect/token"), form, ct);
        tokenResponse.EnsureSuccessStatusCode();
        TokenResponse token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct) ?? throw new InvalidOperationException("Empty token response from Intelliflo.");

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        http.DefaultRequestHeaders.Add("x-api-key", Options.ApiKey);
        Uri baseUri = new(Options.BaseUrl.TrimEnd('/') + "/");
        string clientsPath = externalClientId is null ? "v2/clients" : $"v2/clients/{Uri.EscapeDataString(externalClientId)}";
        ClientsResponse clients = await http.GetFromJsonAsync<ClientsResponse>(new Uri(baseUri, clientsPath), ct) ?? new ClientsResponse([]);

        List<ImportedClient> imported = [];
        foreach (IoClient c in clients.Items)
        {
            PlansResponse plans = await http.GetFromJsonAsync<PlansResponse>(new Uri(baseUri, $"v2/clients/{Uri.EscapeDataString(c.Id)}/plans"), ct) ?? new PlansResponse([]);
            imported.Add(new ImportedClient(
                new ClientWrite { Title = c.Title, FirstName = c.FirstName, LastName = c.LastName, DateOfBirth = c.DateOfBirth, Sex = string.Equals(c.Gender, "Female", StringComparison.OrdinalIgnoreCase) ? Sex.Female : Sex.Male, Email = c.Email },
                c.Id,
                [.. plans.Items.Select(p => new SchemeWrite
                {
                    Type = MapPlanType(p.PlanType),
                    ProductName = p.ProductName ?? p.Provider ?? "Imported plan",
                    PolicyNumber = p.PolicyNumber ?? p.Id,
                    CurrentValue = p.CurrentValue ?? 0m,
                    TransferValue = p.TransferValue ?? p.CurrentValue ?? 0m,
                    ValuationDate = p.ValuationDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                })]));
        }

        return imported;
    }

    private static SchemeType MapPlanType(string? planType) => planType?.ToUpperInvariant() switch
    {
        "SIPP" => SchemeType.Sipp,
        "PERSONAL PENSION" or "PERSONALPENSION" => SchemeType.PersonalPension,
        "STAKEHOLDER" => SchemeType.StakeholderPension,
        "OCCUPATIONAL DB" or "DEFINED BENEFIT" => SchemeType.DefinedBenefit,
        "OCCUPATIONAL DC" or "GROUP PERSONAL PENSION" => SchemeType.OccupationalMoneyPurchase,
        "ISA" => SchemeType.Isa,
        "GIA" or "GENERAL INVESTMENT ACCOUNT" => SchemeType.GeneralInvestmentAccount,
        "ONSHORE BOND" => SchemeType.OnshoreBond,
        "OFFSHORE BOND" => SchemeType.OffshoreBond,
        "DRAWDOWN" => SchemeType.DrawdownPlan,
        _ => SchemeType.PersonalPension,
    };
}

/// <summary>Iress Xplan via the Iress Open standard API (sandbox fixtures only until credentials exist).</summary>
public sealed class XplanConnector(IOptions<IntegrationsOptions> options, ILogger<XplanConnector> logger) : BackOfficeConnectorBase("Xplan", options.Value.Xplan, logger)
{
    protected override Task<IReadOnlyList<ImportedClient>> ImportLiveAsync(string? externalClientId, CancellationToken ct)
    {
        Logger.LogWarning("Xplan Live mode is not implemented; returning no clients.");
        return Task.FromResult<IReadOnlyList<ImportedClient>>([]);
    }
}

/// <summary>True Potential back office (sandbox fixtures only).</summary>
public sealed class TruePotentialConnector(IOptions<IntegrationsOptions> options, ILogger<TruePotentialConnector> logger) : BackOfficeConnectorBase("TruePotential", options.Value.TruePotential, logger)
{
    protected override Task<IReadOnlyList<ImportedClient>> ImportLiveAsync(string? externalClientId, CancellationToken ct)
    {
        Logger.LogWarning("True Potential Live mode is not implemented; returning no clients.");
        return Task.FromResult<IReadOnlyList<ImportedClient>>([]);
    }
}

/// <summary>
/// Origo Integration Hub: Contract Enquiry valuations are synchronous POSTs of Criterion-standard XML to
/// https://oih.origoservices.com/api/getValuation authenticated with a Unipass organisational certificate.
/// Live mode needs the certificate and a provider agreement; Sandbox returns fixtures.
/// </summary>
public sealed class OrigoHubConnector(IOptions<IntegrationsOptions> options, ILogger<OrigoHubConnector> logger) : BackOfficeConnectorBase("OrigoHub", options.Value.OrigoHub, logger)
{
    /// <summary>Builds the Contract Enquiry request envelope (MTG 2.1 header) for a policy; exposed for tests and documentation.</summary>
    public static string BuildContractEnquiryXml(string providerCode, string policyNumber, string adviserUnipassId) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><ContractEnquiryRequest xmlns="http://www.origostandards.com/schema/ce"><Header><MessageVersion>2.1</MessageVersion><Sender>{System.Security.SecurityElement.Escape(adviserUnipassId)}</Sender></Header><Body><Provider>{System.Security.SecurityElement.Escape(providerCode)}</Provider><PolicyNumber>{System.Security.SecurityElement.Escape(policyNumber)}</PolicyNumber></Body></ContractEnquiryRequest>""";

    protected override Task<IReadOnlyList<ImportedClient>> ImportLiveAsync(string? externalClientId, CancellationToken ct)
    {
        Logger.LogWarning("Origo Integration Hub Live mode requires a Unipass certificate; returning no clients.");
        return Task.FromResult<IReadOnlyList<ImportedClient>>([]);
    }
}

/// <summary>
/// Morningstar Direct Web Services: bearer token from POST /token/oauth (Basic auth), then investment/screener
/// endpoints. Sandbox serves the local catalogue so the app works without a licence.
/// </summary>
public sealed class MorningstarFundDataProvider(IHttpClientFactory httpClientFactory, IOptions<IntegrationsOptions> options, IFundCatalogue catalogue, ILogger<MorningstarFundDataProvider> logger) : IFundDataProvider
{
    private readonly IntegrationOptions _options = options.Value.Morningstar;

    public string Name => "Morningstar";
    public IntegrationMode Mode => _options.Mode;
    public DateTime? LastSyncUtc { get; private set; }

    public async Task<IReadOnlyList<FundDto>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        if (Mode == IntegrationMode.Live)
        {
            return await SearchLiveAsync(query, take, ct);
        }

        Page<Fund> page = await catalogue.SearchAsync(query, null, null, 1, Math.Clamp(take, 1, 200), ct);
        return [.. page.Items.Select(f => f.ToDto())];
    }

    public async Task<FundDto?> GetAsync(string isin, CancellationToken ct = default)
    {
        if (Mode == IntegrationMode.Live)
        {
            IReadOnlyList<FundDto> hits = await SearchLiveAsync(isin, 1, ct);
            return hits.Count > 0 ? hits[0] : null;
        }

        Fund? f = await catalogue.GetByIsinAsync(isin, ct);
        return f?.ToDto();
    }

    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        LastSyncUtc = DateTime.UtcNow;
        if (Mode != IntegrationMode.Live)
        {
            return 0; // Sandbox: the catalogue is the source of truth.
        }

        int updated = 0;
        Page<Fund> page = await catalogue.SearchAsync(null, null, null, 1, 200, ct);
        foreach (Fund f in page.Items)
        {
            FundDto? fresh = await GetAsync(f.Isin, ct);
            if (fresh is null)
            {
                continue;
            }

            f.UpdateCharges(Pct.ToFraction(fresh.OcfPct), Pct.ToFraction(fresh.TransactionCostsPct), fresh.AsAt ?? DateOnly.FromDateTime(DateTime.UtcNow), fresh.SourceUrl, DateTime.UtcNow);
            f.UpdateStatistics(new FundStatistics(Pct.ToFraction(fresh.Statistics.Return1YPct), Pct.ToFraction(fresh.Statistics.Return3YPct), Pct.ToFraction(fresh.Statistics.Return5YPct), Pct.ToFraction(fresh.Statistics.Volatility3YPct), fresh.Statistics.Sharpe3Y, Pct.ToFraction(fresh.Statistics.MaxDrawdown3YPct), Pct.ToFraction(fresh.Statistics.YieldPct), fresh.Statistics.MorningstarRating, fresh.Statistics.MedalistRating), fresh.Price, fresh.PriceDate, DateTime.UtcNow);
            updated++;
        }

        return updated;
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record ScreenerResponse(List<ScreenerRow> Rows);

    private sealed record ScreenerRow(string? Isin, string? Name, string? Branding, decimal? OngoingCharge, string? MorningstarCategory, int? StarRating, decimal? Return1Y, decimal? Return3Y, decimal? Return5Y, decimal? StandardDeviation3Y, decimal? Sharpe3Y, decimal? EquityPct, decimal? BondPct, decimal? CashPct);

    private async Task<IReadOnlyList<FundDto>> SearchLiveAsync(string query, int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            logger.LogWarning("Morningstar is set to Live but BaseUrl, ClientId or ClientSecret is missing.");
            return [];
        }

        HttpClient http = httpClientFactory.CreateClient("Morningstar");
        Uri baseUri = new(_options.BaseUrl.TrimEnd('/') + "/");
        using HttpRequestMessage tokenRequest = new(HttpMethod.Post, new Uri(baseUri, "token/oauth"));
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));
        using HttpResponseMessage tokenResponse = await http.SendAsync(tokenRequest, ct);
        tokenResponse.EnsureSuccessStatusCode();
        TokenResponse token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct) ?? throw new InvalidOperationException("Empty token response from Morningstar.");

        using HttpRequestMessage search = new(HttpMethod.Get, new Uri(baseUri, $"screener?term={Uri.EscapeDataString(query)}&pageSize={take}&universe=FOGBR%24%24ALL"));
        search.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using HttpResponseMessage response = await http.SendAsync(search, ct);
        response.EnsureSuccessStatusCode();
        ScreenerResponse rows = await response.Content.ReadFromJsonAsync<ScreenerResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? new ScreenerResponse([]);
        return [.. rows.Rows.Where(r => r.Isin is not null && Holding.IsValidIsin(r.Isin)).Select(r =>
        {
            decimal eq = r.EquityPct ?? 60m;
            decimal bond = r.BondPct ?? Math.Max(0m, 100m - eq - (r.CashPct ?? 0m));
            decimal cash = Math.Max(0m, 100m - eq - bond);
            return new FundDto(null, r.Isin!, null, r.Name ?? r.Isin!, null, r.Branding ?? "Unknown", FundType.Oeic, null, r.MorningstarCategory, r.OngoingCharge ?? 0m, 0m,
                new AssetAllocationDto(eq, bond, 0m, cash, 0m), null,
                new FundStatisticsDto(r.Return1Y, r.Return3Y, r.Return5Y, r.StandardDeviation3Y, r.Sharpe3Y, null, null, r.StarRating, null), null, null, null, _options.BaseUrl, DateOnly.FromDateTime(DateTime.UtcNow));
        })];
    }
}
