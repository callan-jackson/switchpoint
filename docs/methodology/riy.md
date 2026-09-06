# Reduction in yield

COBS 13 Annex 4 3.1R–3.3R (personal and stakeholder pensions) and Annex 3 3.1R (other packaged
products) define RIY algebraically. SwitchPoint implements exactly that construction.

## Definitions
Let `B` be the projection growth rate (the intermediate rate, net of inflation when the projection
is in real terms). Run the `ProjectionEngine` with the full charge schedule at rate `B` to the
projection date to obtain `V_charged`.

- **Product RIY** `A = B − C`, where `C` solves `Project(no charges, rate C) = V_product`, and
  `V_product` is the projection with product, platform and fund charges but **without adviser
  charges** (Annex 4 3.1R).
- **Total RIY** `D = B − E`, where `E` solves `Project(no charges, rate E) = V_all` with every
  charge including adviser charges (Annex 4 3.2R).
- Both `C` and `E` are found with the `RootFinder` (bracket [B − 0.5, B]) and reported unrounded;
  the DTO rounds to the nearest 0.1% (Annex 4 3.1R).

Per-fund RIY (Annex 3 3.3R): when holdings have materially different OCFs (spread > 0.25%) the
calculator returns one RIY per fund as well as the weighted figure.

## Effect of charges table
Annex 4 2.2R columns: year-end, payments in, withdrawals, value before charges, value with plan and
investment charges only, value after all charges. Rows: years 1, 3, 5, then every 5th year, and the
retirement year; for drawdown/UFPLS each of the first ten years. The three value columns are three
projections at rate `B` differing only in the charge schedule. `EffectOfDeductionsToDate =
ValueBeforeCharges − ValueAfterAllCharges` (Annex 3 2.2R note 5).

## Presentation sentence (Annex 4 3.3R)
"Product charges reduce investment growth after price inflation from B% to C%." and "All charges
reduce investment growth from B% to E%." The report renderer uses exactly this wording.

## Comparison use
For a switch, SwitchPoint shows the RIY of each ceding scheme and of the receiving product on the
same term, contributions and growth rate, in real terms (COBS 13 Annex 2 1.2R), so the difference
is attributable to charges alone.
