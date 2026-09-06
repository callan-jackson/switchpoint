# Defined benefit transfer analysis (APTA and TVC)

## Revaluation to retirement (COBS 19 Annex 4B 1R(1), Annex 4C 1R(4))
Each `DbTranche` is revalued from the date of leaving to the date benefits become payable:
- Fixed-rate GMP: scheme rate compounded for complete tax years.
- RPI-linked: 3.0% p.a.; LPI(RPI) capped: min(cap, 3.0%); CPI or LPI(CPI): 2.0%; s148/AEI: 3.5%.
- Statutory revaluation applies for complete years between leaving and NRA (compound).
The revalued pension at NRA is `P_NRA = Σ tranches`. Bridging pensions run only to SPA.

## Transfer Value Comparator (COBS 19.1.3AR, Annex 4B, Annex 4C, Annex 5)
1. Revalue as above to NRA (or the CETV retirement age if past NRA, or the earliest unreduced age
   if available without consent).
2. Price a pension annuity at that age paying `P_NRA` with the tranche escalation (fixed rate,
   RPI/CPI-linked, LPI per the Annex 4C thresholds), spouse's pension at the scheme fraction
   (spouse 3 years younger/older), the scheme guarantee period, PMA16/PFA16-equivalent mortality,
   4.0% expense loading, using the TVC annuity interest rates on the assumption set.
3. Discount from NRA to the calculation date at the FTSE Actuaries fixed-coupon gilt yield for the
   term band (≤5, 5–10, 10–15, >15 years) less the 0.4% p.a. product charge:
   `TVC = AnnuityCost / (1 + gilt − 0.004)^term`.
4. Output Annex 5 wording: "You have been offered a cash equivalent transfer value of £X ... It could
   cost you £Y to obtain a comparable level of income from an insurer. This means the same retirement
   income could cost you £(Y−X) more by transferring." with the two-bar chart data (y-axis from £0)
   and the three prescribed notes.

## Critical yields (optional, PS18/6: "for firms to decide")
Reported for information, labelled as not required by the FCA and not a suitability test:
- **Type A (annuity match)**: growth `x` on the CETV, net of the proposed product's charges and
  initial advice, so that at NRA the fund equals the annuity cost of the scheme benefits (same
  pricing basis as the TVC but with the proposed arrangement's charges).
- **Type B (PCLS + reduced pension)**: fund at NRA equals the scheme PCLS plus the annuity cost of
  the residual pension after commutation at the scheme factor.
- **Hurdle rate (drawdown)**: growth required to sustain the scheme pension (escalating) as drawdown
  income from NRA to the plan end age (default 100, beyond average life expectancy per Annex 4A
  1R(7)) with the fund exhausted at the end.

## APTA outputs (COBS 19 Annex 4A)
- Income comparison at NRA and at ages NRA+5, +10, +15, +20 (real terms at 2.0% CPI): scheme
  pension vs. sustainable drawdown from the transferred fund at the adviser-chosen growth rate that
  reflects the proposed investments, after all charges (Annex 4A 3R).
- Death benefit comparison: scheme spouse's pension (capitalised) vs. residual fund at each age.
- Workplace default comparison when a qualifying scheme exists (Annex 4A 1R(3)).
- Stress tests: growth −2%, inflation +1%, live to 100, fund shock −20% at retirement (Annex 4A 5R).
- One-page summary figures (COBS 9.4.11R): cash-terms first-year and ongoing charges vs. ceding
  scheme and workplace default; initial advice fee payback months = ceil(initial fee /
  revalued monthly DB income discounted at 2.0% CPI).

## Contingent charging (COBS 19.1B)
The DB analysis records the adviser charge basis; a `Contingent` basis raises a compliance warning
unless one of the 19.1B.9R carve-outs is recorded.
