# UK income tax and allowances, 2026/27

Parameters come from `data/tax-years/2026-27.json` (verified 6 September 2026 against gov.uk and
gov.scot). Engines receive a `TaxYearParameters` object; nothing is hard-coded.

## Income tax
1. Adjusted net income `ANI` = total income − gross pension contributions (relief at source and
   net pay both remove the gross amount from ANI for taper purposes) − gift aid.
2. Personal allowance `PA = max(0, 12,570 − 0.5 × max(0, ANI − 100,000))`.
3. Order of taxation: non-savings income, then savings interest, then dividends (ITA 2007 s16).
4. Non-savings: taxable = max(0, income − PA); apply bands cumulatively. Rest of UK: 20% to
   £37,700, 40% to £112,570 (i.e. £125,140 gross), 45% above. Scotland: 19% to £3,967, 20% to
   £16,956, 21% to £31,092, 42% to £62,430, 45% to £112,570, 48% above (widths above the PA).
5. Savings: starting rate band £5,000 at 0%, reduced £1 for £1 by non-savings taxable income;
   personal savings allowance £1,000 / £500 / £0 by highest marginal band; remainder at the
   rUK non-savings rates (Scottish taxpayers pay UK rates on savings). From 6 April 2027 the
   savings rates become 22/42/47% — `TaxYearParameters` carries them so 2027/28 is a data change.
6. Dividends: allowance £500 at 0% (uses band space), then 10.75% / 35.75% / 39.35%.
7. Pension income (DB, annuity, drawdown, 75% of UFPLS) is non-savings income. Tax-free cash is not
   income. The emergency month-1 estimate for a first flexible payment applies 1/12 of the PA and
   bands to the single payment.

## National Insurance (employees only, for pre-retirement cashflow)
Class 1 primary: 8% between £12,570 and £50,270, 2% above. No NI above State Pension age.

## Pension allowances
- Annual allowance £60,000; MPAA £10,000 after flexible access; taper: if threshold income >
  £200,000 and adjusted income > £260,000, AA reduces £1 per £2 over £260,000 to a floor of
  £10,000. Carry forward: unused AA from the three previous years, current year first then oldest.
- Relievable member contributions capped at max(£3,600, relevant UK earnings).
- Lump sum allowance £268,275; LSDBA £1,073,100; tax-free cash 25% of crystallised value capped by
  remaining LSA.
- Normal minimum pension age 55, 57 from 6 April 2028 (`NmpaAt(date)`).

## Capital gains (GIA in cashflow)
Annual exempt amount £3,000; 18% within unused basic-rate band, 24% above. Realised gains are
approximated as `withdrawal × (1 − cost basis fraction)` with the pooled cost basis tracked per GIA.

## Outputs
`TaxComputation` lists PA used, each band's taxable amount and tax, NI, total tax, effective and
marginal rates, and an `Explanation` list of strings so the report can show the working.
