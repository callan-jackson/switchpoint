# Annuity pricing

Both the TVC (COBS 19 Annex 4C 1R(2)) and COBS 13 Annex 2 3.1R price annuities on PMA16/PFA16
mortality with CMI improvements, monthly in advance, a 4% expense loading, and an interest rate
derived from gilt yields. The CMI tables are licensed and cannot be redistributed, so SwitchPoint
ships a **pluggable mortality basis**:

- `ILifeTable.SurvivalProbability(sex, ageFromYears, t)`.
- Default `GompertzMakehamLifeTable` calibrated to the ONS National Life Tables 2020–22 period
  expectations (male 65: 18.5 years, female 65: 21.0 years) with a 1.25% p.a. mortality improvement
  applied by year-of-use. It is an approximation to the PMA16/PFA16 + CMI basis; the report states
  this. A firm holding a CMI licence drops in a `CmiLifeTable` from its own files.

## Annuity factor
For an annual income of £1 starting at age `x`, paid monthly in advance, escalating at `e` per year
(compound, applied annually), guaranteed for `G` years, with a reversion fraction `s` to a spouse
aged `y` (COBS 19 Annex 4C: spouse 3 years younger for a male member, 3 years older for a female
member), at valuation interest rate `i`:

```
a = Σ_{k=0}^{∞} v^(k/12) · (1+e)^floor(k/12) · [ k < 12G  ?  1  :  p_x(k/12) + s · (1 − p_x(k/12)) · p_y(k/12) ] / 12
```
where `v = 1/(1+i)` and `p_x(t)` is the survival probability of the member from age `x` for `t`
years. The sum runs to age 120. The price of income `I` is `I · a · (1 + expense loading)` with the
loading 4.0%.

## Interest rates
- TVC: RPI-linked and level/fixed-increase rates are 3-month averages of the COBS 13 Annex 2 3.1R(6)
  intermediate rates; CPI-linked = RPI-linked + 1.0%; LPI thresholds per Annex 4C 1R(2)(d)–(e).
  They are market inputs stored on the `AssumptionSet` with an as-at date.
- COBS 13 illustrations: `Y = 0.5·(ILG0 + ILG5) − 0.5%` rounded to the nearest 0.2%, level annuities
  at Y + 3.5% (intermediate), RPI-linked at Y.
- AS TM1: single-life, level, 5-year guarantee, 15-year gilt yield rounded to 0.2%.

`AnnuityPricer.Factor(request)` returns the factor, the price per £1, and the components so a test can
check a level single-life factor against a hand calculation on a tiny synthetic life table.
