# SwitchPoint architecture

SwitchPoint is a ground-up build of the kind of analysis platform sold to UK
Independent Financial Advisers (IFAs): an automated, auditable recommendation engine that
compares pension and investment products, quantifies the effect of charges, and produces the
FCA-required analysis (critical yield, reduction in yield, Transfer Value Comparator, cashflow
modelling) behind a suitability report.

This document is the contract between the layers. Anything that changes a public type, a
formula, or an API shape must update this file and the relevant methodology note in
`docs/methodology/`.

## 1. Solution layout

```
SwitchPoint.sln
src/
  SwitchPoint.Domain          entities, value objects, enums, domain rules   (no dependencies)
  SwitchPoint.Calculation     pure, deterministic financial engines          (-> Domain)
  SwitchPoint.Application     use cases, DTOs, validators, port interfaces   (-> Domain, Calculation)
  SwitchPoint.Infrastructure  EF Core, repositories, integrations, Key Vault (-> Application, Domain)
  SwitchPoint.Reports         PDF/DOCX suitability report rendering          (-> Application, Domain)
  SwitchPoint.Api             ASP.NET Core Web API host                      (-> everything)
tests/
  SwitchPoint.<Layer>.Tests   xUnit + FsCheck (property tests) + NSubstitute
web/                          React 19 + TypeScript 6 + Vite 8 SPA
infra/                        Bicep (App Service, Azure SQL free tier, Key Vault, SWA, App Insights)
db/                           reserved for hand-written T-SQL (views, seed helpers); empty today
data/                         seed JSON: providers & charge schedules, funds, tax parameters
docs/                         architecture, methodology notes, ADRs, research brief, OpenAPI export
.github/workflows/            ci.yml (build/test/lint/bicep), deploy.yml (OIDC to Azure)
```

Dependency rule: arrows only point inward (Api -> Infrastructure/Reports -> Application ->
Calculation -> Domain). `Calculation` performs no I/O, has no clock, no randomness except an
explicitly injected seeded generator, and uses `decimal` for every monetary and rate value.

Target framework is .NET 10 LTS (the brief said .NET 8; .NET 8 leaves support in November 2026,
so the current LTS is used; nothing in the code is 10-specific beyond the base packages).

## 2. Bounded contexts and the domain model

All identifiers are `Guid`. All money is `decimal` in GBP. All rates are `decimal` fractions
(0.05m is five per cent), never percentages, at every internal boundary. Percentages appear only
in DTOs that are explicitly suffixed `Pct` and in the UI.

### 2.1 Tenancy and users

- `Firm` (Id, Name, FcaFirmReferenceNumber, DefaultAssumptionSetId, CreatedAtUtc).
- Users are ASP.NET Core Identity users in Infrastructure with claims `firm_id`, `role`.
  Roles: `Adviser`, `Paraplanner`, `Compliance`, `FirmAdmin`, `PlatformAdmin`.
- Every aggregate below carries `FirmId`; repositories filter on it via a global EF query filter.

### 2.2 Client context

- `Client`: Title, FirstName, LastName, DateOfBirth, Sex (`Male|Female`, used for annuity
  pricing only), NationalInsuranceNumber (stored masked, last 3 visible), contact & address,
  MaritalStatus, EmploymentStatus, AnnualSalary, TargetRetirementAge, TaxRegime (`RestOfUk|Scotland`),
  RiskProfile (1..7), Health (`Standard|Enhanced`), Smoker flag, StatePension forecast
  (WeeklyAmount, QualifyingYears), ExternalRef (Source: `Manual|Intelliflo|Xplan|TruePotential|Origo`, Id).
- `Scheme` (an existing arrangement) belongs to a client:
  - `SchemeType`: `PersonalPension | StakeholderPension | Sipp | OccupationalMoneyPurchase |
     DefinedBenefit | Section32 | RetirementAnnuityContract | Isa | GeneralInvestmentAccount |
     OnshoreBond | OffshoreBond | DrawdownPlan`.
  - `ProviderId?`, `ProductName`, `PolicyNumber`, `CurrentValue`, `TransferValue`, `ValuationDate`.
  - `Contributions`: list of `Contribution` (Payer `Member|Employer|ThirdParty`, Amount, Frequency
    `Monthly|Quarterly|Annually|Single`, EscalationRate, IsGrossOfTaxRelief).
  - `Charges`: a `ChargeSchedule` (see 2.4) describing what the scheme actually charges today.
  - `Holdings`: list of (`FundId?` or `Isin`, `Name`, `Weight`).
  - `Guarantees`: GuaranteedAnnuityRate (decimal per £1), GuaranteedGrowthRate, ProtectedTaxFreeCashPct,
    ProtectedPensionAge, WithProfits flag, MarketValueReductionPct, TerminalBonus, LoyaltyBonusPct.
  - `ExitPenalty`: `ExitPenaltySchedule` (see 2.4).
  - `SelectedRetirementAge`.
- `DefinedBenefitScheme` (subtype of scheme, `SchemeType.DefinedBenefit`):
  - DateOfLeaving, NormalRetirementAge, CETV (`CashEquivalentTransferValue`, GuaranteeExpiryDate).
  - `Tranches`: list of `DbTranche` (Name e.g. "Pre-97 excess", AccruedAnnualPension at leaving,
    `RevaluationRule` in deferment, `EscalationRule` in payment).
  - SpousePensionFraction (e.g. 0.5m), GuaranteePeriodYears, PclsCommutationFactor (£ lump sum per
    £1 pension given up), MaxPclsFractionOfCommutedValue, EarlyRetirementReductionPerYear,
    BridgingPensionAnnual (to SPA), SchemeFundingStatus (`Fully|Deficit|PPF`).
- `RevaluationRule` / `EscalationRule`: (`Basis`: `Fixed | Cpi | Rpi | None | LpiCappedCpi`,
  `Rate` (fixed) , `Cap`, `Floor`). Standard presets: GMP fixed rate by leaving date, LPI 5%, LPI 2.5%.

### 2.3 Market context (product and fund catalogue)

- `Provider`: Name, FcaFirmReferenceNumber, Kind (`Platform|Insurer|SippOperator|FundManager|Mps`),
  Website.
- `Product`: ProviderId, Name, `WrapperTypes` (flags: Sipp, PersonalPension, Isa, Gia, Bond,
  Drawdown), MinimumInvestment, MinimumRegularContribution, `ChargeSchedule`, `AllowsFamilyLinking`,
  `AvailableFundUniverse` (`WholeOfMarket|Restricted`), EffectiveFrom, EffectiveTo, SourceUrl, AsAt,
  `DataQuality` (`Verified|Indicative|Placeholder`). A product's charge history is a list of
  versions; analyses pin the version used.
- `Fund`: Isin, Sedol, Name, ShareClass, ManagerName, `FundType` (`Oeic|UnitTrust|Etf|InvestmentTrust|
  ModelPortfolio|Cash`), IaSector, MorningstarCategory, Ocf, TransactionCosts, `AssetAllocation`
  (Equity, FixedInterest, Property, Cash, Alternatives fractions summing to 1), Srri (1..7),
  Volatility3Y, Sharpe3Y, Return1Y/3Y/5Y (annualised), Yield, MaxDrawdown3Y, MorningstarRating (1..5?),
  MedalistRating, PriceDate, Price, FactsheetUrl, SourceUrl, AsAt.
- `ModelPortfolio`: Provider, Name, RiskLevel, MpsFee, Holdings (fund + weight), blended Ocf.

### 2.4 Charge model (shared by schemes and products)

`ChargeSchedule` is a value object:

```
ChargeSchedule
  PlatformCharge: TieredCharge?         annual %, tiered by fund value
  ProductCharge:  TieredCharge?         e.g. insurer AMC, also tiered
  FixedCharges:   List<FixedCharge>     (Amount, Frequency, Indexation: None|Cpi|Fixed(rate), AppliesTo: Wrapper|Drawdown|Sipp)
  FundCharge:     FundChargeBasis       Explicit(ocf) | FromHoldings (weighted OCF of holdings) | None
  TransactionCosts: decimal             annual %, optional
  AdviserCharges: AdviserCharge         InitialPct, InitialAmount, OngoingPct, OngoingAmount (annual)
  DealingCharges: DealingCharges        FundDealAmount, EtfDealAmount, ExpectedDealsPerYear
  SwitchCharge:   decimal               £ per fund switch, ExpectedSwitchesPerYear
  ExitPenalty:    ExitPenaltySchedule   (list of (UntilYearsFromStart, Pct, Amount)) or None
  BidOfferSpreadPct, AllocationRatePct  legacy-plan fields (default 0 and 1)
  LargeFundDiscounts: List<(Threshold, RebatePct)>   negative charges
```

`TieredCharge`: `Bands`: ordered list of (`UpTo` decimal? null = unbounded, `AnnualRate`),
`Mode`: `Marginal` (rate applies to the slice in each band, the UK platform norm) or
`WholeOfFund` (the band containing the total applies to the whole amount). The value object
exposes `AnnualChargeFor(decimal fundValue)` and must be exhaustively tested at boundaries.

### 2.5 Analysis context

- `AssumptionSet` (Id, FirmId?, Name, IsFcaStandard, values): GrowthLower/Intermediate/Higher,
  Inflation, EarningsGrowth, ChargeInflation, PreRetirementDiscountRate, AnnuityInterestRate,
  AnnuityExpenseLoading, MortalityBasis (`OnsNationalLifeTables2020_22`), StatePensionIncrease,
  TaxYear (e.g. "2026/27"), ProjectionBasis (`Nominal|Real`). FCA-standard sets are seeded and
  read-only; firm sets are editable and versioned.
- `PensionSwitchAnalysis`: ClientId, `CedingSchemeIds` (1..n), `ProposedProductId`,
  `ProposedProductVersion`, `ProposedHoldings`, `ProposedAdviserCharges`, RetirementAge,
  AssumptionSetId, `Overrides` (free-form assumption overrides captured for audit), `Result`
  (JSON snapshot of `PensionSwitchResult`), Status (`Draft|Calculated|Locked`), Version, audit fields.
- `DbTransferAnalysis`: ClientId, DbSchemeId, ProposedProductId, ProposedHoldings, TransferDate,
  AssumptionSetId, `Result` (`DbTransferResult`: TVC, critical yields, income comparison), status.
- `CashflowPlan`: ClientId, PartnerClientId?, PlanEndAge (default 100), `Incomes`, `Expenses`
  (phased), `Assets` (links to schemes + non-pension assets), `Strategies` (drawdown rule,
  contribution rule, PCLS choice), `Events` (one-off), AssumptionSetId, `DeterministicResult`,
  `StochasticResult` (percentiles, success probability, seed, paths).
- `Report`: AnalysisId, Kind (`Suitability|PensionSwitch|DbTransfer|Cashflow|FundComparison`),
  Format (`Pdf|Docx|Json`), GeneratedAtUtc, GeneratedBy, Sha256, StoragePath, TemplateVersion.

### 2.6 Audit context

`AuditEvent` is append-only: Id, FirmId, UserId, OccurredAtUtc, EntityType, EntityId, Action,
`Payload` (JSON, redacted of PII fields by policy), `PreviousHash`, `Hash` where
`Hash = SHA256(PreviousHash || canonical JSON of the event without Hash)`. The chain is per firm.
A `GET /audit/verify` endpoint re-walks the chain and reports the first broken link, if any.

## 3. Calculation engine (SwitchPoint.Calculation)

Every engine is a stateless class with a single `Calculate(request) -> result` method, takes only
value types from Domain/Calculation, and is deterministic. All arithmetic is `decimal`.

| Engine | Purpose | Methodology note |
|---|---|---|
| `DecimalMath` | Exp, Ln, Pow, Sqrt, NthRoot on decimal to 1e-20; Brent/bisection root finder | `docs/methodology/numerics.md` |
| `ProjectionEngine` | monthly fund projection with contributions, growth and every charge type in 2.4 | `projection.md` |
| `ReductionInYieldCalculator` | RIY and "effect of charges" tables (COBS 13 Annex 3 style) | `riy.md` |
| `CriticalYieldCalculator` | pension-switch critical yield vs. one or many ceding schemes | `critical-yield.md` |
| `UkTaxCalculator` | 2026/27 income tax (rUK and Scottish), NI, savings/dividend allowances, PA taper | `tax.md` |
| `StatePensionCalculator` | SPA from date of birth; entitlement from qualifying years | `state-pension.md` |
| `AnnuityPricer` | annuity factors from a life table and interest rate; level/escalating/joint/guarantee | `annuity.md` |
| `DbTransferCalculator` | revaluation to NRA, Transfer Value Comparator, critical yields A/B, PCLS options | `db-transfer.md` |
| `CashflowEngine` | annual deterministic household cashflow to plan end age with tax and allowances | `cashflow.md` |
| `MonteCarloSimulator` | correlated lognormal returns via Cholesky; xoshiro256** seeded RNG; percentiles and success probability | `monte-carlo.md` |

Precision rules:

- No `double` in any monetary path. The only `double` allowed is inside the random number
  generator, and its output is converted to `decimal` before any arithmetic with money.
- Annual-to-monthly rate conversion is geometric: `(1 + r)^(1/12) - 1` via `DecimalMath.Pow`.
- Rounding happens only in DTO mapping (2 dp for money, 4 dp for rates when expressed as a
  percentage, i.e. 0.0525m -> "5.25%").
- Root finders stop at |f(x)| < 0.005 (half a penny) or a bracket width < 1e-10 and must report
  the iteration count and whether they converged; a non-converged result is an error, never a
  silently returned value.

## 4. Application layer

Ports (interfaces in `SwitchPoint.Application.Ports`):

- `IClientRepository`, `ISchemeRepository`, `IProductCatalogue`, `IFundCatalogue`,
  `IAnalysisRepository<T>`, `IAssumptionSetRepository`, `IReportStore`, `IAuditLog`,
  `IUnitOfWork`, `IClock`, `ICurrentUser`.
- Integrations: `IBackOfficeConnector` (Intelliflo, Xplan, True Potential, Origo Hub) with
  `ImportClientsAsync`, `ImportSchemesAsync(clientRef)`; `IFundDataProvider` (Morningstar) with
  `SearchAsync`, `GetAsync(isin)`, `SyncAsync()`; `IValuationProvider` (Origo Contract Enquiry).
- `IReportRenderer` (`RenderAsync(ReportRequest) -> ReportDocument`).

Use cases are plain classes named `<Verb><Noun>Handler` with a single `HandleAsync`. Validation
is FluentValidation; failures become RFC 9457 problem details. Every handler that mutates data
writes an audit event in the same unit of work.

## 5. API

- Base path `/api/v1`. JSON, camelCase, `DateOnly` as ISO dates, decimals as numbers.
- Auth: JWT bearer. Development issues tokens from `POST /api/v1/auth/login` against seeded users
  (Identity). Production can swap to Entra ID by configuration; the claims contract is fixed.
- Resources: `auth`, `firms`, `clients`, `clients/{id}/schemes`, `products`, `providers`, `funds`,
  `model-portfolios`, `assumption-sets`, `analyses/pension-switch`, `analyses/db-transfer`,
  `analyses/cashflow` (+ `/stochastic`), `calculations/*` (stateless preview endpoints used by the
  UI for live recalculation; no persistence, still audited at info level), `reports`, `audit`,
  `integrations/{connector}/import`, `health`.
- OpenAPI generated by `Microsoft.AspNetCore.OpenApi`, served at `/openapi/v1.json` with Scalar UI
  at `/scalar`. The committed export lives at `docs/api/openapi.json`; CI fails if the running
  document differs from the committed one (drift check).
- Rate limiting on `calculations/*` (per user), request size caps, security headers, CORS
  restricted to the SPA origin.

## 6. Persistence

EF Core with SQL Server in production and SQLite for tests and local development without Docker
(provider chosen by `Database:Provider`). **No EF migrations have been generated yet**: on both
providers the schema is created with `EnsureCreated` at startup when `Database:MigrateOnStartup=true`
(the default). That is fine for a database that is only ever created fresh, but it cannot upgrade one
in place, so `dotnet ef migrations add InitialCreate` is a prerequisite for the first production
upgrade. The startup path already prefers `Migrate()` once migrations exist. Owned/complex types: `ChargeSchedule`, `TieredCharge`,
`AssumptionSet` values are stored as JSON columns (`ToJson()`) to keep the tier structure faithful;
seed data comes from `data/*.json` through idempotent seeders keyed on natural keys (ISIN,
provider+product name+effective date).

## 7. Reports

QuestPDF renders A4 PDFs with: cover, executive summary, client snapshot, analysis inputs and
assumptions (with the assumption set name and version), charge comparison tables, critical yield /
RIY charts (SVG generated by `SwitchPoint.Reports.Charts`), cashflow fan chart, fund annex, audit
statement (analysis id, hash, generated timestamp, engine version). DOCX export uses OpenXML with
the same section model. Every report embeds the SHA-256 of its JSON source so a regulator can prove
the document matches the stored analysis.

## 8. Front end

React 19, TypeScript 6, Vite 8, React Router 7, TanStack Query, Recharts, Tailwind 4, Zod,
react-hook-form. Types are generated from `docs/api/openapi.json` with `openapi-typescript`. Pages:
Dashboard, Clients, Client detail (schemes, imports), Pension Switch (live preview), DB Transfer,
Cashflow (deterministic table + Monte Carlo fan chart), Fund Research (screen/compare), Products &
charges explorer, Reports, Audit, Settings (assumption sets). Every calculation page debounces
input changes and calls the stateless `calculations/*` endpoints for the live preview, then
persists via the analysis endpoints when the adviser saves.

## 9. Infrastructure and delivery

- Bicep in `infra/` provisions: Log Analytics + Application Insights, App Service plan (Linux,
  `F1` default), Web App for Containers pulling `ghcr.io/callan-jackson/switchpoint-api`, Azure SQL
  (serverless, `useFreeLimit` where offered), Key Vault (RBAC; the web app's managed identity is a
  Key Vault Secrets User), Static Web App (Free) for the SPA. Secrets (SQL connection string, JWT
  signing key, Morningstar/Intelliflo credentials) live in Key Vault and are read through
  `Azure.Extensions.AspNetCore.Configuration.Secrets`.
- GitHub Actions: `ci.yml` builds and tests both stacks, checks OpenAPI drift, lints Bicep;
  `deploy.yml` (OIDC federated credential, no stored secrets) builds and pushes the API container,
  deploys infra with `what-if` then `create`, updates the web app image, runs the EF migration
  bundle, and publishes the SPA.

## 10. Non-functional requirements

- Determinism: identical inputs produce byte-identical results (tests assert this, including for
  Monte Carlo given a seed).
- Performance: a pension switch analysis with 3 ceding schemes over 40 years returns in < 50 ms;
  a 1,000-path Monte Carlo over 60 years in < 2 s on one core.
- Security: no PII in logs; NI numbers masked at rest; firm isolation enforced at the query
  filter, not the controller; audit chain verifiable.
- Compliance posture: the software calculates and documents; the adviser remains responsible for
  the recommendation. The FCA references in `docs/methodology/` say exactly which rule each
  number implements and the date it was checked.
