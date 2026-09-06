# Numerics

## Types
- All money and rates are `decimal`. Rates are fractions (0.05m = 5%).
- `double` is permitted only inside the pseudo-random generator; conversion to `decimal` happens
  before any arithmetic with money.

## DecimalMath
`Exp(x)`, `Ln(x)`, `Pow(x, y)`, `Sqrt(x)`, `NthRoot(x, n)` on `decimal` with absolute error
below 1e-20 for |x| ≤ 100. Implementation: range reduction (`Exp` by halving/squaring, `Ln` by
`ln(m·2^k) = ln m + k·ln 2` with `m` in [0.5, 1)) then Taylor/Newton iteration until the term is
below 1e-28. `Pow(x, y) = Exp(y·Ln(x))` for x > 0; `Pow(0, y>0) = 0`; `Pow(x, 0) = 1`; negative
base throws.

Rate helpers:
- `AnnualToMonthly(r) = (1 + r)^(1/12) − 1`
- `MonthlyToAnnual(m) = (1 + m)^12 − 1`
- `Real(nominal, inflation) = (1 + nominal)/(1 + inflation) − 1` (Fisher, exact form)

## RootFinder
`Solve(Func<decimal, decimal> f, decimal lo, decimal hi, RootOptions)` finds x with f(x)=0.
- Requires f(lo) and f(hi) of opposite sign; otherwise expands the bracket geometrically up to
  `MaxBracketExpansions` (default 20) and then throws `RootNotBracketedException`.
- Method: Brent's method (inverse quadratic interpolation with bisection fallback).
- Stops when |f(x)| < `ValueTolerance` (default 0.005m, half a penny) or bracket width <
  `ArgumentTolerance` (default 1e-10m). Maximum 200 iterations, then throws `RootNotConvergedException`.
- Returns `RootResult(Value, Iterations, Converged)`; callers never receive an unconverged value.

## Rounding
- Internal: none.
- Presentation (DTO mapping): money `MidpointRounding.ToEven` to 2 dp unless a rule says otherwise;
  RIY to nearest 0.1% (COBS 13 Annex 4 3.1R: "rounded to the nearest 0.1%") using
  `MidpointRounding.AwayFromZero`; standardised projection values rounded **down** to 3
  significant figures (COBS 13 Annex 2 1.2R) via `RoundDownToSignificant(value, 3)`.
