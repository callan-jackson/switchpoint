# SwitchPoint

**FCA-compliant pension switching, defined benefit transfer and cashflow analysis platform for UK
Independent Financial Advisers.** An auditable recommendation engine, built ground-up, that compares
pension and investment products across the market, quantifies the effect of charges to the penny,
runs the analyses the FCA Handbook requires (critical yield, reduction in yield, Transfer Value
Comparator, APTA, cashflow and stochastic modelling) and produces the suitability report behind a
recommendation.

| Layer | Stack |
|---|---|
| Calculation engines | C# / .NET 10, `decimal` arithmetic throughout, 366 unit and property tests |
| API | ASP.NET Core Web API, JWT auth, OpenAPI + Scalar, RFC 9457 problem details, rate limiting |
| Persistence | EF Core (Azure SQL in production, SQLite locally), JSON-column value objects, hash-chained audit log |
| Front end | React 19, TypeScript 6, Vite 8, Tailwind 4, TanStack Query, Recharts |
| Reports | QuestPDF (PDF), OpenXML (DOCX), deterministic JSON with SHA-256 embedded in every document |
| Cloud | Azure App Service (container), Azure SQL serverless free offer, Key Vault, Application Insights, Bicep, GitHub Actions with OIDC |

**Live:** <https://switchpoint-callan.azurewebsites.net> — sign in as `adviser@demo.switchpoint.local` /
`Demo!Pass123` (paraplanner and compliance demo accounts use the same password). The API documentation is at
[`/scalar`](https://switchpoint-callan.azurewebsites.net/scalar). It runs on the free App Service tier against a
serverless database that pauses when idle, so the first request after a quiet spell takes a minute or so.

## What it does

| Capability an adviser needs | SwitchPoint module | Where |
|---|---|---|
| Pension Switching (critical yield, RIY, effect of charges, consolidation of several plans) | `CriticalYieldCalculator`, `ReductionInYieldCalculator`, `ProjectionEngine` | `src/SwitchPoint.Calculation/CriticalYield`, `/Riy`, `/Projection` |
| Defined Benefit Transfer ("APTA with TVC"), Income Modeller, hurdle rate, PCLS options | `DbTransferCalculator`, `AnnuityPricer`, life table | `src/SwitchPoint.Calculation/DbTransfer`, `/Annuities`, `/Mortality` |
| Cashflow and Drawdown, stress tests, randomiser | `CashflowEngine`, `MonteCarloSimulator` | `src/SwitchPoint.Calculation/Cashflow`, `/MonteCarlo` |
| Portfolio Insight (fund and MPS research, compare portfolios) | Fund catalogue, Morningstar adapter, fund research pages | `src/SwitchPoint.Infrastructure/Integrations`, `web/src/pages/FundResearch` |
| Provider charge database, Bespoke Plans | Versioned product charge schedules with tiered, fixed, dealing, exit and legacy charges | `src/SwitchPoint.Domain/Charges`, `data/providers.json` |
| Back-office integrations (Intelliflo iO, Xplan, True Potential), Origo Integration Hub | `IBackOfficeConnector` implementations (live + sandbox) | `src/SwitchPoint.Infrastructure/Integrations` |
| Report writing (Word/PDF, archived reports) | Suitability, pension switch, DB transfer, cashflow and fund comparison renderers | `src/SwitchPoint.Reports` |
| Audit trail | Per-firm SHA-256 hash chain with a verification endpoint | `src/SwitchPoint.Domain/Audit`, `/api/v1/audit/verify` |
| Tax and allowance rules | 2026/27 income tax (rest-of-UK and Scottish), NI, annual allowance taper, MPAA, LSA, State Pension age | `src/SwitchPoint.Calculation/Tax`, `/StatePension` |

## Regulatory anchoring

Every number the engines use is traced to its source in `docs/methodology/` and
`data/fca-assumptions.json` / `data/tax-years/2026-27.json`, verified on 6 September 2026:

- **Transfer Value Comparator**: COBS 19.1.3AR, Annex 4B/4C (RPI 3.0% / CPI 2.0% / s148 3.5% revaluation, 4% annuity expenses, spouse three years younger/older, gilt-yield discounting less the 0.4% charge introduced by PS20/6) and the Annex 5 wording and two-bar chart.
- **Projections and RIY**: COBS 13 Annex 2 (2% / 5% / 8% nominal, 2.0% intermediate inflation since 19 November 2025, values rounded down to three significant figures) and Annex 3/4 (RIY = B − C to the nearest 0.1%; effect-of-charges table at years 1, 3, 5 and retirement).
- **Switching suitability**: the FSA 2009 pension switching template's four unsuitable outcomes drive the per-scheme verdicts (Switch candidate / Consider / Retain / Refer).
- **Contingent charging**: COBS 19.1B carve-outs are recorded on every DB analysis.
- **Stochastic modelling**: COBS 19.1.2CR conservativeness check (median no less conservative than the deterministic analysis).

The market research behind the build — what the established UK analysis tools do, which regulatory
analyses they run and how advisers use them — is in `docs/research/`.

## Running locally

```bash
# API (SQLite, demo data seeded, Scalar UI at http://localhost:5080/scalar)
dotnet run --project src/SwitchPoint.Api

# Web (proxies /api to :5080; or set VITE_USE_MOCKS=true to run without the API)
cd web && npm install --legacy-peer-deps && npm run dev

# Tests
dotnet test
cd web && npm test
```

Demo login: `adviser@demo.switchpoint.local` / `Demo!Pass123`.

Optional full stack with SQL Server: `docker compose up` (see `docker-compose.yml`).

## Deployment

`infra/main.bicep` provisions the whole environment on free tiers; `.github/workflows/deploy.yml`
builds the container, pushes it to GitHub Container Registry and deploys with an OIDC federated
credential (no stored cloud secrets). One-time setup steps are in `docs/deployment.md`.

## Repository layout

```
src/SwitchPoint.Domain          entities, value objects, charge model, audit chain
src/SwitchPoint.Calculation     projection, RIY, critical yield, tax, State Pension, annuity, DB transfer, cashflow, Monte Carlo
src/SwitchPoint.Application     ports, DTOs, validators, use-case handlers, calculation orchestration
src/SwitchPoint.Infrastructure  EF Core, migrations, seeders, integrations, Key Vault, audit log
src/SwitchPoint.Reports         PDF / DOCX / JSON report rendering
src/SwitchPoint.Api             ASP.NET Core host
web/                            React SPA
infra/                          Bicep
data/                           seed data and verified regulatory parameters
docs/                           architecture, methodology, ADRs, API contract, research
```

## Status and limitations

This is a production-shaped portfolio system, not a regulated product. Provider charge data and
fund data are seeded from published sources with an as-at date and a data-quality flag
(`Verified` / `Indicative` / `Placeholder`); a live deployment would need licensed Morningstar and
Origo Integration Hub connections and a CMI mortality licence (the shipped life table is a documented
Gompertz–Makeham fit to ONS National Life Tables). See `docs/STATUS.md` for the current state of
each layer.

## Licence

MIT. Copyright © 2026 Callan Jackson.
