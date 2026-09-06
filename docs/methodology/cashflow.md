# Cashflow modelling (deterministic)

Annual steps from the plan start (calculation date) to the plan end age (default 100) for a
household of one or two people. Nominal engine, with real-terms presentation at the assumption
set's inflation (2.0% when used inside an APTA, Annex 4A 5R).

## Inputs
- People: date of birth, sex, tax regime, retirement age, State Pension (forecast or qualifying years).
- Incomes: employment (amount, growth rate = earnings assumption, end age), self-employment, rental,
  DB pensions (amount at NRA from the DB engine, escalation), annuities, other; each with start/end.
- Expenses: phases (e.g. "to retirement", "active retirement", "later life") each with an annual
  amount in today's money inflated by CPI, and one-off events (inflows or outflows in a given year).
- Assets: pensions (uncrystallised DC, drawdown, DB), ISAs, GIAs (with cost basis), cash, property
  (value only, optional downsizing event). Each asset has a growth rate (from holdings or explicit)
  and a `ChargeSchedule`.
- Contributions: per asset, with employer contributions, salary sacrifice flag, escalation.
- Strategy: withdrawal order (default: cash → GIA → ISA → pension drawdown, configurable), pension
  crystallisation choice (`PclsUpFront`, `PhasedUfpls`, `PhasedDrawdown`), drawdown rule
  (`GapFill` = withdraw net of tax what is needed to meet expenses; `FixedAmount`; `PercentOfPot`),
  surplus rule (reinvest into ISA up to the allowance, then GIA), annuity purchase at an age (optional).

## Year loop
For each plan year `y` and each person:
1. Age up; determine State Pension start (SPA date), retirement, NMPA (55/57) access eligibility.
2. Accrue incomes (employment stops at retirement age); apply earnings growth.
3. Apply contributions (employee relief-at-source grossed up; employer; AA/MPAA check → warning).
4. Grow assets net of charges using the `ProjectionEngine` for 12 months.
5. Compute the net income need = inflated expenses + one-off outflows − guaranteed net income.
6. Draw from assets per the withdrawal order, iterating on tax: a withdrawal `W` from a pension
   yields `W × 0.25` tax-free (up to remaining LSA; `PclsUpFront` crystallises 25% at first access)
   and the rest is taxed with all other income via `UkTaxCalculator`; the engine solves for the
   gross withdrawal that leaves the required net amount using the `RootFinder` (tax is piecewise
   linear so this converges in a few steps).
7. Record a shortfall if assets are exhausted; invest surplus per the surplus rule.
8. Record the year row: per source gross income, tax, NI, net income, expenses, surplus/shortfall,
   each asset's closing value, remaining LSA, AA used, warnings.

## Outputs
Rows per year, first shortfall age, total tax paid, legacy (estate value) at each age, and
sustainable withdrawal rate (the constant real income the assets support to the plan end at the
assumed growth, found with the `RootFinder`). Stress scenarios rerun the loop with adjusted
assumptions and are returned side by side.
