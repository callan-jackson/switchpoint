# Projection engine

Projects a single arrangement forward month by month from the calculation date to a target date
(normally the selected retirement age) applying growth, contributions and every charge type in the
`ChargeSchedule`. It is the primitive used by RIY, critical yield, APTA and cashflow.

## Inputs (`ProjectionRequest`)
- `StartValue` (transfer value or current value, after any exit penalty if `ApplyExitPenaltyAtStart`).
- `Contributions`: list of (Payer, Amount, Frequency, EscalationRate, EscalationMonth, StartMonth,
  EndMonth, GrossOfTaxRelief). Relief-at-source contributions are grossed up by 20% when
  `GrossOfTaxRelief=false`.
- `GrowthRate` annual nominal (fund growth before charges).
- `Charges`: `ChargeSchedule`.
- `WeightedOcf`, `WeightedTransactionCosts` derived from holdings when the schedule says `FromHoldings`.
- `Months` (term), `Basis` Nominal or Real (with `Inflation`), `ChargeInflation` for indexed fixed fees.
- `InitialAdviserChargeTiming`: `DeductedFromTransfer` (default) or `PaidSeparately` (excluded).
- `Tiering`: `PerPlan` or `Household(totalHouseholdValue)` for family-linked platform tiers.

## Order of operations per month `t` (1..Months)
1. Contributions received for month `t` (monthly every month; quarterly at months 1,4,7,10 of each
   plan year; annual at month 1 of each plan year; single at `StartMonth`), less initial adviser
   charge % on contributions if configured, less `(1 − AllocationRate)` and bid/offer spread for
   legacy plans.
2. Growth for the month: `V *= (1 + m)` where `m = AnnualToMonthly(GrowthRate)`. Contributions
   received in month `t` earn a full month of growth (received at the start of the month). Charges
   are taken at the end of the month, after growth.
3. Percentage charges for the month, each computed on the month-end value **before** charges and
   converted with `annualRate / 12` (simple twelfth, matching how UK platforms accrue daily/monthly
   and how COBS 13 Annex 2 2.2R "compounded annually" is reproduced within 0.01% at 12 steps):
   platform (tiered), product AMC (tiered), fund OCF, transaction costs, ongoing adviser %.
   Large-fund discounts are negative platform charges.
4. Fixed charges due in month `t` (monthly / quarterly / annual as above), indexed by
   `ChargeInflation` each plan year if `Indexation != None`; drawdown fees only when
   `InDrawdown`.
5. Dealing and switch charges spread evenly: `(FundDealAmount × ExpectedDealsPerYear + SwitchCharge × ExpectedSwitchesPerYear) / 12`.
6. Value floors at zero; if charges exceed value the shortfall is recorded in `UnpaidCharges` and the
   projection continues at zero (a legacy plan with a fixed policy fee can exhaust itself).
7. At the final month, if `ApplyExitPenaltyAtEnd`, the exit penalty schedule is applied and reported
   separately as `ExitPenaltyAtEnd`.

## Real terms
When `Basis = Real`, the nominal engine runs unchanged and each reported value is deflated by
`(1 + Inflation)^(t/12)`. This matches COBS 13 Annex 2 1.2R (pensions shown in today's prices at
the intermediate inflation rate, 2.0% from 19 November 2025) and AS TM1 B.8.1.

## Outputs (`ProjectionResult`)
- `FinalValue`, `FinalValueReal`.
- `Schedule`: one row per plan year (and the final month) with Value, ContributionsToDate,
  GrowthToDate, and cumulative charges by category (`ChargeBreakdown`).
- `TotalCharges`, `TotalContributions`, `UnpaidCharges`, `ExitPenaltyAtEnd`.
- `EffectOfDeductions = ValueWithoutCharges − Value` requires a second run with an empty schedule;
  `ProjectionEngine.ProjectWithAndWithoutCharges` does both and guarantees identical contribution
  and growth handling.

## Invariants (tested)
- Zero growth, zero charges, no contributions ⇒ FinalValue = StartValue exactly.
- Charges monotonically reduce FinalValue; more months of a positive-growth, zero-charge projection
  never reduce value.
- Nominal 5% for 12 months with no charges = StartValue × 1.05 within 1e-10.
- Percentage charge of `c` annual with no growth over 12 months ≈ `1 − (1 − c/12)^12` of value.
