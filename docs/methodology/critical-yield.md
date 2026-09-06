# Critical yield (pension switching, no safeguarded benefits)

There is no Handbook formula for a DC-to-DC critical yield; the FSA's 2009 switching template
compares charges and RIY, and the "critical yield" is an industry convention that Selectapension,
O&M Profiler and others report. SwitchPoint defines it precisely so the number is reproducible.

## Definition
Given one or more ceding schemes projected to the selected retirement date at growth rate `g`
(their own charge schedules, continuing contributions, no exit penalty because they are retained)
with total `V_existing`, and a receiving product whose start value is
`Σ(transfer value_i − exit penalty_i) − initial adviser charge` with the same continuing
contributions redirected to it:

**Critical yield** `x*` is the annual growth rate the receiving product's investments must earn,
before its charges, so that `Project_new(x*) = V_existing`.

- Found by `RootFinder` on `f(x) = Project_new(x) − V_existing`, bracket `[g − 0.10, g + 0.10]`,
  expanded if needed.
- **Headroom** `= g − x*`. Positive headroom means the switch is expected to be ahead at the assumed
  growth rate; negative means the receiving product needs to outperform.
- Also reported: `ProjectedGain = Project_new(g) − V_existing`, break-even year (first plan year at
  which `Project_new(g)` ≥ `Project_existing(g)`), and the same three figures at the lower and
  higher COBS 13 rates (2% and 8% nominal; 5% intermediate) shown in real terms at 2.0% inflation.

## Consolidation of several ceding schemes
`V_existing` is the sum of the individually projected schemes; each keeps its own charge schedule,
guarantees and contributions. Schemes with guarantees (GAR, guaranteed growth, with-profits,
protected tax-free cash > 25%, protected pension age) are flagged and excluded from the automatic
"switch" verdict; a guaranteed growth rate is used as the floor of that scheme's growth.

## Verdict rules (surface in the report, adviser decides)
Per ceding scheme: `Retain` when the receiving product's total RIY is higher and there is no
non-cost reason recorded; `Consider` when RIY is within 0.1%; `Switch candidate` when the receiving
RIY is lower and headroom is positive; always `Refer` when a guarantee flag is set. These map to the
four unsuitable outcomes in the FSA template (extra cost, lost guarantees, ATR mismatch, no review).
