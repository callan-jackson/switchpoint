# Calculation methodology

Each note below is the specification for one engine in `SwitchPoint.Calculation`. The notes say
which FCA rule (if any) a number implements, what SwitchPoint does where the Handbook leaves the
method to the firm, and which values are market inputs that must be refreshed from a data feed.

| Note | Engine | Regulatory anchor |
|---|---|---|
| [numerics.md](numerics.md) | `DecimalMath`, `RootFinder` | none (engineering) |
| [projection.md](projection.md) | `ProjectionEngine` | COBS 13 Annex 2 (2.2R, 2.6R, 2.7G) |
| [riy.md](riy.md) | `ReductionInYieldCalculator` | COBS 13 Annex 3 3.1R, Annex 4 3.1R–3.3R, 2.2R |
| [critical-yield.md](critical-yield.md) | `CriticalYieldCalculator` | FSA pension switching template (2009); industry convention |
| [tax.md](tax.md) | `UkTaxCalculator` | ITA 2007 rates as published on gov.uk / gov.scot for 2026/27 |
| [state-pension.md](state-pension.md) | `StatePensionCalculator` | Pensions Act 2014; SPA timetable |
| [annuity.md](annuity.md) | `AnnuityPricer` | COBS 13 Annex 2 3.1R basis; COBS 19 Annex 4C 1R(2) |
| [db-transfer.md](db-transfer.md) | `DbTransferCalculator` | COBS 19.1, Annex 4A/4B/4C/5; COBS 9.4.11R |
| [cashflow.md](cashflow.md) | `CashflowEngine` | COBS 19 Annex 4A 5R (real terms, stress tests) |
| [monte-carlo.md](monte-carlo.md) | `MonteCarloSimulator` | COBS 19.1.2CR (50th percentile no less conservative) |

Verified-on dates and source URLs for every parameter live in `data/tax-years/2026-27.json` and
`data/fca-assumptions.json`. The research behind them is in `docs/research/`.
