# Smoke test

A record of the API being exercised end to end from a clean database. Run on 7 September 2026 against the
Debug build on .NET 10, with SQLite and the demo firm seeded. Responses are trimmed to the interesting fields.

```bash
cd src/SwitchPoint.Api/bin/Debug/net10.0
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__SwitchPoint="Data Source=/tmp/sp-smoke.db" \
ASPNETCORE_URLS=http://localhost:5093 \
Reports__Path=/tmp/sp-reports \
dotnet SwitchPoint.Api.dll
```

The API creates the schema, seeds the catalogue and the demo firm, and reports healthy in about four seconds:

```
[INF] Ensuring database schema exists (Microsoft.EntityFrameworkCore.Sqlite)
[INF] Seeding from /Users/cj/switchpoint/data (demo: True)
$ curl -s http://localhost:5093/healthz
Healthy
```

## Sign in

```bash
TOKEN=$(curl -s -X POST $B/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"adviser@demo.switchpoint.local","password":"Demo!Pass123"}' | jq -r .accessToken)
curl -s $B/auth/me -H "Authorization: Bearer $TOKEN"
```

```json
{"id":"0b0593b9-…","displayName":"Alex Adviser","email":"adviser@demo.switchpoint.local",
 "role":"adviser","firmId":"11111111-1111-1111-1111-111111111111","firmName":"Demo Financial Planning Ltd"}
```

## Clients and their arrangements

```bash
curl -s $B/clients -H "$H"
```

| Client | Schemes | Pension value |
|---|---|---|
| Ms Sarah Mitchell | 2 | £238,500 |
| Mr David Okafor | 2 | £696,000 |
| Dr Priya Shah | 2 | £265,000 |

Sarah's schemes come back with the net transfer value and weighted ongoing charge computed:

```
Employer Group Personal Pension   £96,500 current   £96,500 transferable   OCF 0.22%
Legacy Personal Pension (with-profits) £142,000 current £138,000 transferable (guaranteed annuity rate 8.5%)
```

## Products

```bash
curl -s $B/products -H "$H"
```

| Provider | Product | Charge at £100k | Charge at £500k |
|---|---|---|---|
| abrdn | abrdn Wrap SIPP | 0.35% | 0.31% |
| AJ Bell Investcentre | Investcentre SIPP | 0.20% | 0.175% |
| Aviva | Aviva Platform Pension Portfolio | 0.375% | 0.305% |

## Pension switch preview

Both of Sarah's plans against the AJ Bell SIPP holding Vanguard LifeStrategy 60, 1% initial and 0.5% ongoing
adviser charges, to age 67:

```bash
curl -s -X POST $B/calculations/pension-switch -H "$H" -H 'Content-Type: application/json' -d @request.json
```

| Measure | Value |
|---|---|
| Critical yield (lower / intermediate / higher) | 1.678% / 4.669% / 7.660% |
| Headroom at the intermediate rate | +0.331% |
| Net transfer value | £234,500 |
| Reduction in yield on the proposed plan | 1.013% |

Per scheme, with the reduction in yield if retained versus if switched:

```
Employer Group Personal Pension          retain  0.701% → 1.018%
Legacy Personal Pension (with-profits)   refer   2.229% → 1.041%
```

```
"warnings": ["'Legacy Personal Pension (with-profits)' has guarantees or protected features
              (FSA switching outcome 2); it is referred for adviser judgement."]
```

The workplace plan is cheaper where it is, so the engine says retain it; the legacy plan is much dearer but
carries a guaranteed annuity rate, so it is referred rather than recommended. Thirteen chart points are returned
for the projection, and the assumption set used is named in the result.

## Defined benefit transfer preview

David's Wealden Engineering scheme (CETV £486,000, four tranches, normal retirement age 65):

| Measure | Value |
|---|---|
| Scheme pension at retirement | £30,973 a year |
| Transfer Value Comparator | £426,902 |
| Gilt yield used (5–10 year band) | 4.4% |
| Critical yield: annuity match / with tax-free cash / drawdown hurdle | 3.73% / 3.60% / 5.24% |
| Sustainable income from the transfer (today's money) | £20,842 |
| Months of pension needed to pay the £9,000 advice fee | 5 |

## Cashflow and stochastic modelling

Priya, 44, £84,000 salary, £265,000 SIPP, £42,000 spending, plan to 95:

```
rows 51   succeeds true   first shortfall none   sustainable spend £43,610   estate at 95 £272,409 (today's money)
stochastic: 100 paths, seed 7 → probability of success 0.50
```

## Tax

```bash
curl -s -X POST $B/calculations/tax -H "$H" -d '{"regime":"restOfUk","earnedIncome":20000,"subjectToNi":true}'
```

```json
{"incomeTax":1486,"nationalInsurance":594.40,"marginalRatePct":20,"taxYear":"2026/27"}
```

£20,000 less the £12,570 personal allowance is £7,430 at 20% = £1,486; National Insurance is 8% of the same
£7,430 = £594.40. Both match the published 2026/27 figures.

## Audit trail

```bash
curl -s $B/audit/verify -H "Authorization: Bearer $COMPLIANCE_TOKEN"
```

```json
{"isValid":true,"firstBrokenIndex":-1,"reason":null,"eventsChecked":14}
```

## Reports

The API integration tests cover the full document path (`AnalysisAndReportTests`): create a pension switch
analysis, calculate it, generate a PDF report, and download it. The response is `application/pdf`, starts with
`%PDF`, and exceeds 10 KB; generating the report locks the analysis, after which further edits return 409.
Sample PDFs for all five report kinds are written to
`tests/SwitchPoint.Reports.Tests/bin/Debug/net10.0/samples/` when the report tests run (98–136 KB each).
