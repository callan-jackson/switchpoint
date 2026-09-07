# Status

Last updated: 7 September 2026.

## Test counts

| Project | Tests | Covers |
|---|---|---|
| SwitchPoint.Domain.Tests | 94 | charge model boundaries, exit penalties, audit hash chain, analysis lifecycle, DB tranches, ISIN validation, property tests |
| SwitchPoint.Calculation.Tests | 195 | decimal numerics, root finding, projection, RIY, critical yield, tax, State Pension, annuity pricing, DB transfer, cashflow, Monte Carlo |
| SwitchPoint.Application.Tests | 16 | DTO mappers and percent conversion, validators, calculation orchestration, analysis lifecycle handlers, firm scoping |
| SwitchPoint.Infrastructure.Tests | 11 | EF Core round trips of JSON value objects, tenant filter, audit append/verify/tamper, seeding idempotence, connectors, report store |
| SwitchPoint.Reports.Tests | 19 | PDF/DOCX/JSON rendering of all five report kinds, composer section order, SVG chart edge cases |
| SwitchPoint.Api.Tests | 26 | auth and roles, client and scheme CRUD, catalogue, all calculation endpoints, analysis lifecycle, reports, audit, rate limiting, OpenAPI drift |
| **Total** | **361** | |

`dotnet test` runs them all; every project is green with warnings treated as errors.

## Layer status

| Layer | State |
|---|---|
| Domain | Complete. Entities, value objects, the full charge model, DB scheme tranches with revaluation and escalation rules, versioned product charges, assumption sets, analyses, tamper-evident audit chain. |
| Calculation | Complete. `DecimalMath`/`RootFinder`/`RateMath`, deterministic RNG, projection engine, RIY, critical yield, UK tax (rest-of-UK and Scottish 2026/27), State Pension age and entitlement, annuity pricing on a documented mortality basis, DB transfer (TVC, APTA, critical yields, stress tests), cashflow, Monte Carlo. |
| Application | Complete. Ports, DTOs for the whole API contract, bidirectional mappers, calculation orchestration, use-case handlers for clients, schemes, catalogue, assumptions, analyses, reports, audit and integrations, FluentValidation validators, deterministic JSON and hashing. |
| Infrastructure | Complete. EF Core (SQL Server and SQLite) with JSON-column value objects, per-firm query filters, ASP.NET Core Identity, hash-chained append-only audit log, idempotent seeders, back-office and Morningstar connectors (live shapes plus sandbox fixtures), Key Vault configuration, report file store. |
| Reports | Complete. QuestPDF A4 PDFs, OpenXML DOCX, deterministic JSON, SVG charts, five report kinds including the COBS 19 Annex 5 Transfer Value Comparator layout and the COBS 13 Annex 4 effect-of-charges table. |
| API | Complete. JWT auth with role policies, 45 endpoints, RFC 9457 problem details, per-user rate limiting, security headers, correlation ids, health checks, OpenAPI plus Scalar, SPA hosting with client-side routing fallback. |
| Web | In progress. API client, typed contract, component library and design tokens exist; pages and tests are being finished. |
| Infrastructure as code | Complete and validated against Azure (`az deployment group validate` succeeds): Log Analytics, Application Insights, Linux App Service plan, Web App for Containers, Azure SQL serverless on the free offer, Key Vault with RBAC and a Secrets User role assignment for the app identity. |
| CI/CD | Workflows for build/test/lint/bicep/docker, OIDC deployment to Azure, CodeQL and Dependabot. |

## Known gaps

- **Front end.** The shell, API client and component library are in place; the ten pages and their tests are still being completed.
- **Seed data.** The catalogue currently falls back to a small built-in set (7 providers, 7 funds) marked `Indicative`/`Placeholder`. The cited `data/providers.json`, `data/funds.json` and `data/model-portfolios.json` files with 20+ verified provider charge sheets and 60+ funds are being compiled.
- **Mortality.** The shipped life table is a Gompertz–Makeham fit to ONS National Life Tables 2020–22, documented as an approximation to the licensed PMA16/PFA16 with CMI improvements that COBS 19 Annex 4C prescribes. A firm holding a CMI licence can drop in a real table behind `ILifeTable`.
- **Market inputs.** FTSE Actuaries gilt yields and the COBS 13 annuity rate `Y` are not published as numbers in the Handbook. They are stored on the assumption set with an as-at date and must be refreshed monthly from a licensed feed.
- **Integrations.** Intelliflo has a real OAuth2 and REST implementation but is untested against the live service (no credentials); Xplan, True Potential and Origo have sandbox fixtures and documented live shapes. Origo needs a Unipass organisational certificate.
- **Migrations.** SQL Server migrations have not been generated; the API creates the schema with `EnsureCreated` on both providers. Add `dotnet ef migrations add InitialCreate` before the first production upgrade.
