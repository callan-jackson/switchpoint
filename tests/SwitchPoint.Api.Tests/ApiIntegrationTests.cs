using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Infrastructure.Persistence;

namespace SwitchPoint.Api.Tests;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;

[Collection("api")]
public class AuthAndSecurityTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_openapi_and_scalar_are_anonymous()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/healthz", UriKind.Relative))).StatusCode);
        HttpResponseMessage openapi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, openapi.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await openapi.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").EnumerateObject().Any());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/scalar", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/clients", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/products", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Login_succeeds_for_the_demo_users_and_fails_on_a_bad_password()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        HttpResponseMessage ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "adviser@demo.switchpoint.local", password = "Demo!Pass123" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        LoginResponse login = (await ok.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json))!;
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal("Demo Financial Planning Ltd", login.User.FirmName);
        Assert.True(login.ExpiresAtUtc > DateTime.UtcNow);

        HttpResponseMessage bad = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "adviser@demo.switchpoint.local", password = "wrong-password" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("application/problem+json", bad.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_returns_the_authenticated_user()
    {
        UserDto user = (await fixture.Adviser.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiFactory.Json))!;
        Assert.Equal("adviser@demo.switchpoint.local", user.Email);
        Assert.Equal(Domain.Tenancy.UserRole.Adviser, user.Role);
    }

    [Fact]
    public async Task Security_headers_and_the_correlation_id_are_present()
    {
        HttpResponseMessage response = await fixture.Adviser.GetAsync(new Uri("/api/v1/clients", UriKind.Relative));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Single(response.Headers.GetValues("X-Correlation-Id"));

        using HttpRequestMessage echo = new(HttpMethod.Get, "/api/v1/clients");
        echo.Headers.Add("X-Correlation-Id", "abc123");
        HttpResponseMessage echoed = await fixture.Adviser.SendAsync(echo);
        Assert.Equal("abc123", echoed.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Roles_gate_the_protected_endpoints()
    {
        using HttpClient compliance = await fixture.Factory.AuthenticatedClientAsync("compliance@demo.switchpoint.local");
        Assert.Equal(HttpStatusCode.OK, (await compliance.GetAsync(new Uri("/api/v1/audit/verify", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Adviser.GetAsync(new Uri("/api/v1/audit/verify", UriKind.Relative))).StatusCode);

        IReadOnlyList<AssumptionSetDto> sets = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<AssumptionSetDto>>("/api/v1/assumption-sets", ApiFactory.Json))!;
        HttpResponseMessage copy = await fixture.Adviser.PostAsJsonAsync($"/api/v1/assumption-sets/{sets[0].Id}/copy", new { name = "Firm defaults" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, copy.StatusCode);
    }
}

[Collection("api")]
public class ClientAndCatalogueTests(ApiFixture fixture)
{
    [Fact]
    public async Task Demo_clients_and_their_schemes_are_returned()
    {
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients", ApiFactory.Json))!;
        Assert.Equal(3, page.Total);
        ClientSummary sarah = page.Items.Single(c => c.FullName.EndsWith("Mitchell", StringComparison.Ordinal));
        Assert.Equal(2, sarah.SchemeCount);

        ClientDetail detail = (await fixture.Adviser.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{sarah.Id}", ApiFactory.Json))!;
        Assert.Equal(2, detail.Schemes.Count);
        Assert.All(detail.Schemes, s => Assert.True(s.NetTransferValue > 0m));
        Assert.Contains(detail.Schemes, s => s.Guarantees.GuaranteedAnnuityRatePct > 0m);
        Assert.Equal("******56A", detail.NationalInsuranceNumberMasked);
        Assert.Null(detail.NationalInsuranceNumber);

        PagedResult<ClientSummary> filtered = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Okafor", ApiFactory.Json))!;
        Assert.Equal(1, filtered.Total);
    }

    [Fact]
    public async Task Client_crud_round_trip_with_validation()
    {
        object write = new
        {
            title = "Mr",
            firstName = "Test",
            lastName = "Client",
            dateOfBirth = "1980-05-01",
            sex = "male",
            targetRetirementAge = 65,
            riskProfile = 4,
            statePension = new { qualifyingYears = 20 },
        };
        HttpResponseMessage created = await fixture.Adviser.PostAsJsonAsync("/api/v1/clients", write, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        ClientDetail client = (await created.Content.ReadFromJsonAsync<ClientDetail>(ApiFactory.Json))!;
        Assert.Equal("Mr Test Client", client.FullName);

        HttpResponseMessage updated = await fixture.Adviser.PutAsJsonAsync($"/api/v1/clients/{client.Id}", new { title = "Mr", firstName = "Test", lastName = "Client", dateOfBirth = "1980-05-01", sex = "male", targetRetirementAge = 67, riskProfile = 5, statePension = new { qualifyingYears = 21 } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(67, (await updated.Content.ReadFromJsonAsync<ClientDetail>(ApiFactory.Json))!.TargetRetirementAge);

        HttpResponseMessage invalid = await fixture.Adviser.PostAsJsonAsync("/api/v1/clients", new { firstName = "", lastName = "X", dateOfBirth = "2030-01-01", sex = "male", riskProfile = 9 }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out JsonElement errors));
        Assert.True(errors.EnumerateObject().Any());

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Adviser.DeleteAsync(new Uri($"/api/v1/clients/{client.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Adviser.GetAsync(new Uri($"/api/v1/clients/{client.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scheme_with_tiered_charges_persists_and_is_returned()
    {
        HttpResponseMessage createdOwner = await fixture.Adviser.PostAsJsonAsync("/api/v1/clients", new { firstName = "Scheme", lastName = "Owner", dateOfBirth = "1975-01-01", sex = "female", targetRetirementAge = 66, riskProfile = 4, statePension = new { } }, ApiFactory.Json);
        ClientDetail client = (await createdOwner.Content.ReadFromJsonAsync<ClientDetail>(ApiFactory.Json))!;

        object scheme = new
        {
            type = "sipp",
            productName = "Test SIPP",
            policyNumber = "T-1",
            currentValue = 200_000,
            transferValue = 200_000,
            valuationDate = "2026-08-31",
            startDate = "2022-01-01",
            charges = new
            {
                platformCharge = new { mode = "marginal", bands = new object[] { new { upTo = 250_000, annualRatePct = 0.25 }, new { upTo = (decimal?)null, annualRatePct = 0.15 } } },
                fundCharge = new { kind = "fromHoldings", ocfPct = (decimal?)null },
                adviserCharges = new { initialPct = 1, initialAmount = 0, ongoingPct = 0.5, ongoingAmount = 0 },
                exitPenalty = new { bands = new object[] { new { untilYearsFromStart = 5, ratePct = 2, amount = 0 } } },
                allocationRatePct = 100,
            },
            holdings = new object[] { new { name = "Vanguard LifeStrategy 60% Equity", weightPct = 100, isin = "GB00B3TYHH97" } },
            contributions = new object[] { new { payer = "member", amount = 300, frequency = "monthly", escalationPct = 3, isGrossOfTaxRelief = true } },
        };
        HttpResponseMessage created = await fixture.Adviser.PostAsJsonAsync($"/api/v1/clients/{client.Id}/schemes", scheme, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        SchemeDto dto = (await created.Content.ReadFromJsonAsync<SchemeDto>(ApiFactory.Json))!;
        Assert.Equal(0.25m, dto.Charges.PlatformCharge!.Bands[0].AnnualRatePct);
        Assert.Equal(196_000m, dto.NetTransferValue); // 2% exit penalty within five years
        Assert.Equal(0.22m, dto.WeightedOcfPct);

        HttpResponseMessage typeChange = await fixture.Adviser.PutAsJsonAsync($"/api/v1/clients/{client.Id}/schemes/{dto.Id}", new { type = "isa", productName = "Test", currentValue = 1, transferValue = 1, valuationDate = "2026-08-31" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, typeChange.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Adviser.DeleteAsync(new Uri($"/api/v1/clients/{client.Id}/schemes/{dto.Id}", UriKind.Relative))).StatusCode);
        await fixture.Adviser.DeleteAsync(new Uri($"/api/v1/clients/{client.Id}", UriKind.Relative));
    }

    [Fact]
    public async Task Another_firms_client_is_not_found()
    {
        Guid otherFirm = Guid.NewGuid();
        Guid otherClientId = Guid.NewGuid();
        await fixture.Factory.WithDbAsync(async db =>
        {
            db.Clients.Add(new Client(otherClientId, otherFirm, "Other", "Firm", new DateOnly(1980, 1, 1), Sex.Male, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Adviser.GetAsync(new Uri($"/api/v1/clients/{otherClientId}", UriKind.Relative))).StatusCode);
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients", ApiFactory.Json))!;
        Assert.DoesNotContain(page.Items, c => c.Id == otherClientId);
    }

    [Fact]
    public async Task Catalogue_endpoints_return_seeded_data()
    {
        IReadOnlyList<ProviderDto> providers = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ProviderDto>>("/api/v1/providers", ApiFactory.Json))!;
        Assert.True(providers.Count >= 5);

        IReadOnlyList<ProductSummary> products = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ProductSummary>>("/api/v1/products", ApiFactory.Json))!;
        Assert.All(products, p => Assert.NotNull(p.EffectiveChargePctAt100k));
        ProductSummary ajBell = products.First(p => p.ProviderName.StartsWith("AJ Bell", StringComparison.Ordinal));
        Assert.True(ajBell.EffectiveChargePctAt500k < ajBell.EffectiveChargePctAt100k);

        ProductDetail detail = (await fixture.Adviser.GetFromJsonAsync<ProductDetail>($"/api/v1/products/{ajBell.Id}", ApiFactory.Json))!;
        Assert.NotEmpty(detail.ChargeVersions);
        Assert.NotNull(detail.ChargeVersions[0].Charges.PlatformCharge);

        IReadOnlyList<ProductSummary> sipps = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ProductSummary>>("/api/v1/products?wrapper=Sipp", ApiFactory.Json))!;
        Assert.All(sipps, p => Assert.Contains("Sipp", p.WrapperTypes));

        PagedResult<FundDto> funds = (await fixture.Adviser.GetFromJsonAsync<PagedResult<FundDto>>("/api/v1/funds?pageSize=5", ApiFactory.Json))!;
        Assert.True(funds.Total >= 5);
        Assert.True(funds.Items.Count <= 5);
        FundDto fund = (await fixture.Adviser.GetFromJsonAsync<FundDto>("/api/v1/funds/GB00B3TYHH97", ApiFactory.Json))!;
        Assert.Equal(0.22m, fund.OcfPct);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Adviser.GetAsync(new Uri("/api/v1/funds/GB00B0000000", UriKind.Relative))).StatusCode);

        IReadOnlyList<AssumptionSetDto> sets = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<AssumptionSetDto>>("/api/v1/assumption-sets", ApiFactory.Json))!;
        Assert.True(sets[0].IsFcaStandard);
        Assert.Equal(5m, sets[0].GrowthIntermediatePct);
        Assert.Equal("2026/27", sets[0].TaxYear);

        IReadOnlyList<IntegrationStatusDto> integrations = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<IntegrationStatusDto>>("/api/v1/integrations", ApiFactory.Json))!;
        Assert.Contains(integrations, i => i.Connector == "Intelliflo" && i.Mode == "Sandbox");
    }

    [Fact]
    public async Task Dashboard_summary_counts_the_firms_data()
    {
        using JsonDocument doc = JsonDocument.Parse(await fixture.Adviser.GetStringAsync(new Uri("/api/v1/dashboard/summary", UriKind.Relative)));
        Assert.True(doc.RootElement.GetProperty("clients").GetInt32() >= 3);
        Assert.True(doc.RootElement.GetProperty("fundsInCatalogue").GetInt32() >= 5);
        Assert.True(doc.RootElement.TryGetProperty("recentAnalyses", out _));
    }

    [Fact]
    public async Task Sandbox_import_creates_clients_from_the_back_office()
    {
        // Its own host: importing changes the client list other tests assert on.
        using ApiFactory factory = new();
        using HttpClient client = await factory.AuthenticatedClientAsync();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/integrations/Intelliflo/import", new { }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ImportResult result = (await response.Content.ReadFromJsonAsync<ImportResult>(ApiFactory.Json))!;
        Assert.Equal(2, result.Imported + result.Updated);
        PagedResult<ClientSummary> page = (await client.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Whitfield", ApiFactory.Json))!;
        Assert.Equal(1, page.Total);
    }
}

[Collection("api")]
public class CalculationTests(ApiFixture fixture)
{
    private async Task<(Guid ClientId, IReadOnlyList<SchemeDto> Schemes)> SarahAsync()
    {
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Mitchell", ApiFactory.Json))!;
        ClientDetail detail = (await fixture.Adviser.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{page.Items[0].Id}", ApiFactory.Json))!;
        return (detail.Id, detail.Schemes);
    }

    private async Task<Guid> ProductIdAsync()
    {
        IReadOnlyList<ProductSummary> products = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ProductSummary>>("/api/v1/products?wrapper=Sipp", ApiFactory.Json))!;
        return products.First(p => p.ProviderName.StartsWith("AJ Bell", StringComparison.Ordinal)).Id;
    }

    private static object SwitchRequest(Guid clientId, IEnumerable<Guid> schemeIds, Guid productId) => new
    {
        clientId,
        cedingSchemes = schemeIds.Select(id => new { schemeId = id }).ToArray(),
        proposedProductId = productId,
        proposedHoldings = new object[] { new { name = "Vanguard LifeStrategy 60% Equity", weightPct = 100, isin = "GB00B3TYHH97" } },
        proposedAdviserCharges = new { initialPct = 1, initialAmount = 0, ongoingPct = 0.5, ongoingAmount = 0 },
        retirementAge = 67,
        redirectContributions = true,
    };

    [Fact]
    public async Task Pension_switch_preview_returns_ordered_critical_yields_and_percentages()
    {
        (Guid clientId, IReadOnlyList<SchemeDto> schemes) = await SarahAsync();
        HttpResponseMessage response = await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/pension-switch", SwitchRequest(clientId, schemes.Select(s => s.Id), await ProductIdAsync()), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PensionSwitchResultDto result = (await response.Content.ReadFromJsonAsync<PensionSwitchResultDto>(ApiFactory.Json))!;

        Assert.Equal(2m, result.Lower.GrowthPct);
        Assert.Equal(5m, result.Intermediate.GrowthPct);
        Assert.Equal(8m, result.Higher.GrowthPct);
        Assert.True(result.Lower.CriticalYieldPct < result.Intermediate.CriticalYieldPct);
        Assert.True(result.Intermediate.CriticalYieldPct < result.Higher.CriticalYieldPct);
        Assert.Equal(2, result.Schemes.Count);
        Assert.Contains(result.Schemes, s => s.Verdict == Calculation.CriticalYield.SwitchVerdict.Refer);
        Assert.True(result.AnyGuaranteesFlagged);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("investment growth", result.ReceivingRiy.ProductSentence, StringComparison.Ordinal);
        Assert.True(result.Chart.Count > 5);
        Assert.Equal("1.0.0", result.EngineVersion);
        Assert.Equal("FCA standard 2026/27", result.AssumptionSet.Name);
    }

    [Fact]
    public async Task Invalid_holdings_are_rejected_with_problem_details()
    {
        (Guid clientId, IReadOnlyList<SchemeDto> schemes) = await SarahAsync();
        object request = new
        {
            clientId,
            cedingSchemes = schemes.Select(s => new { schemeId = s.Id }).ToArray(),
            proposedProductId = await ProductIdAsync(),
            proposedHoldings = new object[] { new { name = "Half a fund", weightPct = 50, isin = "GB00B3TYHH97" } },
            proposedAdviserCharges = new { initialPct = 1, initialAmount = 0, ongoingPct = 0.5, ongoingAmount = 0 },
            retirementAge = 67,
        };
        HttpResponseMessage response = await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/pension-switch", request, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("100%", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Db_transfer_preview_produces_the_prescribed_comparator()
    {
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Okafor", ApiFactory.Json))!;
        ClientDetail david = (await fixture.Adviser.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{page.Items[0].Id}", ApiFactory.Json))!;
        SchemeDto dbScheme = david.Schemes.Single(s => s.Type == SchemeType.DefinedBenefit);

        object request = new
        {
            dbSchemeId = dbScheme.Id,
            proposedProductId = await ProductIdAsync(),
            proposedHoldings = new object[] { new { name = "Vanguard LifeStrategy 60% Equity", weightPct = 100, isin = "GB00B3TYHH97" } },
            proposedAdviserCharges = new { initialPct = 2, initialAmount = 0, ongoingPct = 0.75, ongoingAmount = 0 },
            aptaGrowthPct = 5,
            planEndAge = 100,
            initialAdviceFee = 9000,
            workplaceDefaultChargePct = 0.75,
        };
        DbTransferResultDto result = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/db-transfer", request, ApiFactory.Json)).Content.ReadFromJsonAsync<DbTransferResultDto>(ApiFactory.Json))!;

        Assert.Equal(486_000m, result.Tvc.CashEquivalentTransferValue);
        Assert.Contains("cash equivalent transfer value", result.Tvc.Wording, StringComparison.Ordinal);
        Assert.Equal(3, result.Tvc.Notes.Count);
        Assert.Equal(4, result.Tvc.Tranches.Count);
        Assert.Equal(65, result.Tvc.RetirementAgeUsed);
        Assert.True(result.Tvc.EstimatedReplacementCost > 0m);
        Assert.True(result.CriticalYields.Converged);
        Assert.Equal(5, result.IncomeComparison.Count);
        Assert.Equal(5, result.StressTests.Count);
        Assert.True(result.Summary.PaybackMonths > 0);
    }

    [Fact]
    public async Task Cashflow_and_stochastic_previews_are_deterministic()
    {
        object plan = new
        {
            person = new { name = "Priya", dateOfBirth = "1982-09-02", sex = "female", taxRegime = "restOfUk", retirementAge = 60, statePensionQualifyingYears = 22, mpaaTriggered = false },
            planEndAge = 95,
            incomes = new object[] { new { name = "Salary", kind = "employment", annualAmount = 84_000, fromAge = 40, growthPct = 3.5, isTaxable = true, personIndex = 0 } },
            expenses = new object[] { new { name = "Living", annualAmount = 42_000, fromAge = 40 } },
            assets = new object[] { new { name = "SIPP", kind = "uncrystallisedPension", value = 265_000, growthPct = 5, charges = new { }, costBasis = 0, annualContribution = 8_400, employerContribution = 6_720, salarySacrifice = false, personIndex = 0, allocation = new { equityPct = 80, fixedInterestPct = 20, propertyPct = 0, cashPct = 0, alternativesPct = 0 } } },
            events = Array.Empty<object>(),
            strategy = new { withdrawalOrder = new[] { "cash", "isa", "drawdown", "uncrystallisedPension" }, crystallisation = "phasedDrawdown", drawdownRule = "gapFill", drawdownParameter = 0, reinvestSurplusIntoIsa = true },
            seed = 7,
            paths = 100,
        };

        CashflowResultDto deterministic = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/cashflow", plan, ApiFactory.Json)).Content.ReadFromJsonAsync<CashflowResultDto>(ApiFactory.Json))!;
        Assert.Equal(51, deterministic.Rows.Count);
        Assert.True(deterministic.SustainableSpend > 0m);
        Assert.Equal(44, deterministic.Rows[0].Age);

        StochasticResultDto first = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/cashflow/stochastic", plan, ApiFactory.Json)).Content.ReadFromJsonAsync<StochasticResultDto>(ApiFactory.Json))!;
        StochasticResultDto second = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/cashflow/stochastic", plan, ApiFactory.Json)).Content.ReadFromJsonAsync<StochasticResultDto>(ApiFactory.Json))!;
        Assert.Equal("7", first.Seed);
        Assert.Equal(100, first.Paths);
        Assert.Equal(first.ProbabilityOfSuccess, second.ProbabilityOfSuccess);
        Assert.Equal(first.TotalAssetsReal[^1].P50, second.TotalAssetsReal[^1].P50);
        Assert.All(first.TotalAssetsReal, r => Assert.True(r.P5 <= r.P50 && r.P50 <= r.P95));
    }

    [Fact]
    public async Task Tax_preview_matches_the_published_2026_27_figures()
    {
        TaxComputationDto tax = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/tax", new { regime = "restOfUk", earnedIncome = 20_000, subjectToNi = true }, ApiFactory.Json)).Content.ReadFromJsonAsync<TaxComputationDto>(ApiFactory.Json))!;
        Assert.Equal(1_486m, tax.IncomeTax);
        Assert.Equal(594.40m, tax.NationalInsurance);
        Assert.Equal(20m, tax.MarginalRatePct);
        Assert.Equal("2026/27", tax.TaxYear);

        TaxComputationDto scottish = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/tax", new { regime = "scotland", earnedIncome = 50_000, subjectToNi = false }, ApiFactory.Json)).Content.ReadFromJsonAsync<TaxComputationDto>(ApiFactory.Json))!;
        Assert.Equal(8_982.05m, Math.Round(scottish.IncomeTax, 2));
        Assert.Equal(42m, scottish.MarginalRatePct);
    }

    [Fact]
    public async Task Riy_preview_returns_the_cobs_sentences()
    {
        object request = new
        {
            startValue = 100_000,
            months = 240,
            growthPct = 5,
            charges = new { platformCharge = new { mode = "marginal", bands = new object[] { new { upTo = (decimal?)null, annualRatePct = 0.75 } } } },
            contributions = Array.Empty<object>(),
            inflationPct = 2,
        };
        RiyDto riy = (await (await fixture.Adviser.PostAsJsonAsync("/api/v1/calculations/riy", request, ApiFactory.Json)).Content.ReadFromJsonAsync<RiyDto>(ApiFactory.Json))!;
        Assert.InRange(riy.ProductRiyPct, 0.78m, 0.79m);
        Assert.Equal(riy.ProductRiyPct, riy.TotalRiyPct);
        Assert.Contains("Product charges reduce investment growth", riy.ProductSentence, StringComparison.Ordinal);
        Assert.Equal(6, riy.EffectOfCharges.Count);
    }
}

[Collection("api")]
public class AnalysisAndReportTests(ApiFixture fixture)
{
    private async Task<PensionSwitchAnalysisDto> CreateAndCalculateAsync()
    {
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Mitchell", ApiFactory.Json))!;
        ClientDetail sarah = (await fixture.Adviser.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{page.Items[0].Id}", ApiFactory.Json))!;
        IReadOnlyList<ProductSummary> products = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ProductSummary>>("/api/v1/products?wrapper=Sipp", ApiFactory.Json))!;
        IReadOnlyList<AssumptionSetDto> sets = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<AssumptionSetDto>>("/api/v1/assumption-sets", ApiFactory.Json))!;

        object write = new
        {
            clientId = sarah.Id,
            title = "Consolidate two plans",
            retirementAge = 67,
            cedingSchemeIds = sarah.Schemes.Select(s => s.Id).ToArray(),
            proposedProductId = products.First(p => p.ProviderName.StartsWith("AJ Bell", StringComparison.Ordinal)).Id,
            proposedProductChargeVersion = 1,
            proposedHoldings = new object[] { new { name = "Vanguard LifeStrategy 60% Equity", weightPct = 100, isin = "GB00B3TYHH97" } },
            proposedAdviserCharges = new { initialPct = 1, initialAmount = 0, ongoingPct = 0.5, ongoingAmount = 0 },
            assumptionSetId = sets[0].Id,
            rationale = "Lower ongoing charges and access to drawdown.",
        };
        HttpResponseMessage created = await fixture.Adviser.PostAsJsonAsync("/api/v1/analyses/pension-switch", write, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        PensionSwitchAnalysisDto analysis = (await created.Content.ReadFromJsonAsync<PensionSwitchAnalysisDto>(ApiFactory.Json))!;
        Assert.Equal(AnalysisStatus.Draft, analysis.Status);

        HttpResponseMessage calculated = await fixture.Adviser.PostAsync(new Uri($"/api/v1/analyses/pension-switch/{analysis.Id}/calculate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, calculated.StatusCode);
        return (await calculated.Content.ReadFromJsonAsync<PensionSwitchAnalysisDto>(ApiFactory.Json))!;
    }

    [Fact]
    public async Task Create_calculate_lock_and_report_flow()
    {
        PensionSwitchAnalysisDto analysis = await CreateAndCalculateAsync();
        Assert.Equal(AnalysisStatus.Calculated, analysis.Status);
        Assert.Equal(1, analysis.Version);
        Assert.NotNull(analysis.ResultHash);
        Assert.Equal(64, analysis.ResultHash!.Length);
        Assert.NotNull(analysis.Result);
        Assert.True(analysis.Result!.Intermediate.CriticalYieldPct > 0m);

        HttpResponseMessage json = await fixture.Adviser.PostAsJsonAsync("/api/v1/reports", new { analysisId = analysis.Id, kind = "pensionSwitch", format = "json" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, json.StatusCode);
        ReportDto report = (await json.Content.ReadFromJsonAsync<ReportDto>(ApiFactory.Json))!;
        Assert.Equal(64, report.Sha256.Length);
        Assert.Equal(analysis.ResultHash, report.AnalysisResultHash);

        HttpResponseMessage download = await fixture.Adviser.GetAsync(new Uri(report.DownloadUrl, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/json", download.Content.Headers.ContentType?.MediaType);
        Assert.Contains(analysis.ResultHash, await download.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Issuing a report locks the analysis.
        PensionSwitchAnalysisDto locked = (await fixture.Adviser.GetFromJsonAsync<PensionSwitchAnalysisDto>($"/api/v1/analyses/pension-switch/{analysis.Id}", ApiFactory.Json))!;
        Assert.Equal(AnalysisStatus.Locked, locked.Status);

        HttpResponseMessage update = await fixture.Adviser.PutAsJsonAsync($"/api/v1/analyses/pension-switch/{analysis.Id}", new { clientId = analysis.ClientId, title = "Changed", retirementAge = 66, assumptionSetId = analysis.AssumptionSetId }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Adviser.DeleteAsync(new Uri($"/api/v1/analyses/pension-switch/{analysis.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Pdf_report_downloads_as_a_pdf()
    {
        PensionSwitchAnalysisDto analysis = await CreateAndCalculateAsync();
        HttpResponseMessage created = await fixture.Adviser.PostAsJsonAsync("/api/v1/reports", new { analysisId = analysis.Id, kind = "suitability", format = "pdf" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ReportDto report = (await created.Content.ReadFromJsonAsync<ReportDto>(ApiFactory.Json))!;
        Assert.True(report.SizeBytes > 10_000);

        HttpResponseMessage download = await fixture.Adviser.GetAsync(new Uri(report.DownloadUrl, UriKind.Relative));
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Draft_analyses_can_be_deleted_and_uncalculated_ones_cannot_be_reported()
    {
        PagedResult<ClientSummary> page = (await fixture.Adviser.GetFromJsonAsync<PagedResult<ClientSummary>>("/api/v1/clients?search=Shah", ApiFactory.Json))!;
        IReadOnlyList<AssumptionSetDto> sets = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<AssumptionSetDto>>("/api/v1/assumption-sets", ApiFactory.Json))!;
        HttpResponseMessage created = await fixture.Adviser.PostAsJsonAsync("/api/v1/analyses/pension-switch", new { clientId = page.Items[0].Id, title = "Draft", retirementAge = 65, assumptionSetId = sets[0].Id }, ApiFactory.Json);
        PensionSwitchAnalysisDto draft = (await created.Content.ReadFromJsonAsync<PensionSwitchAnalysisDto>(ApiFactory.Json))!;

        HttpResponseMessage report = await fixture.Adviser.PostAsJsonAsync("/api/v1/reports", new { analysisId = draft.Id, kind = "pensionSwitch", format = "json" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, report.StatusCode);

        HttpResponseMessage calculate = await fixture.Adviser.PostAsync(new Uri($"/api/v1/analyses/pension-switch/{draft.Id}/calculate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.BadRequest, calculate.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Adviser.DeleteAsync(new Uri($"/api/v1/analyses/pension-switch/{draft.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Audit_records_the_analysis_events_and_the_chain_verifies()
    {
        await CreateAndCalculateAsync();
        PagedResult<AuditEventDto> events = (await fixture.Adviser.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/v1/audit?pageSize=100", ApiFactory.Json))!;
        Assert.Contains(events.Items, e => e.Action == "Created" && e.EntityType == "PensionSwitchAnalysis");
        Assert.Contains(events.Items, e => e.Action == "Calculated");
        Assert.All(events.Items, e => Assert.Equal(64, e.Hash.Length));

        using HttpClient compliance = await fixture.Factory.AuthenticatedClientAsync("compliance@demo.switchpoint.local");
        ChainVerificationDto verification = (await compliance.GetFromJsonAsync<ChainVerificationDto>("/api/v1/audit/verify", ApiFactory.Json))!;
        Assert.True(verification.IsValid);
        Assert.Equal(-1, verification.FirstBrokenIndex);
        Assert.True(verification.EventsChecked > 0);
    }

    [Fact]
    public async Task Reports_are_listed_for_the_client()
    {
        PensionSwitchAnalysisDto analysis = await CreateAndCalculateAsync();
        await fixture.Adviser.PostAsJsonAsync("/api/v1/reports", new { analysisId = analysis.Id, kind = "pensionSwitch", format = "json" }, ApiFactory.Json);
        IReadOnlyList<ReportDto> byClient = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ReportDto>>($"/api/v1/clients/{analysis.ClientId}/reports", ApiFactory.Json))!;
        Assert.NotEmpty(byClient);
        IReadOnlyList<ReportDto> all = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<ReportDto>>("/api/v1/reports", ApiFactory.Json))!;
        Assert.NotEmpty(all);
        IReadOnlyList<AnalysisSummary> analyses = (await fixture.Adviser.GetFromJsonAsync<IReadOnlyList<AnalysisSummary>>($"/api/v1/clients/{analysis.ClientId}/analyses", ApiFactory.Json))!;
        Assert.Contains(analyses, a => a.Kind == AnalysisKind.PensionSwitch);
    }
}

public class RateLimitAndDocumentTests
{
    [Fact]
    public async Task Calculation_endpoints_are_rate_limited()
    {
        using ApiFactory factory = new(new Dictionary<string, string?> { ["RateLimiting:CalculationsPerMinute"] = "3" });
        using HttpClient client = await factory.AuthenticatedClientAsync();
        object request = new { regime = "restOfUk", earnedIncome = 20_000, subjectToNi = true };
        List<HttpStatusCode> codes = [];
        for (int i = 0; i < 8; i++)
        {
            codes.Add((await client.PostAsJsonAsync("/api/v1/calculations/tax", request, ApiFactory.Json)).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
        Assert.Equal(HttpStatusCode.OK, codes[0]);
    }

    [Fact]
    public async Task Committed_openapi_document_matches_the_running_api()
    {
        using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        string live = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        string path = Path.Combine(RepoRoot(), "docs", "api", "openapi.json");
        Assert.True(File.Exists(path), $"docs/api/openapi.json is missing at {path}");
        string committed = await File.ReadAllTextAsync(path);
        Assert.Equal(Normalise(committed), Normalise(live));
    }

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    /// <summary>Compares documents ignoring the server URL, which depends on how the host was started.</summary>
    private static string Normalise(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> root = document.RootElement.EnumerateObject().Where(p => p.Name != "servers").ToDictionary(p => p.Name, p => p.Value);
        return JsonSerializer.Serialize(root, Compact);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SwitchPoint.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
