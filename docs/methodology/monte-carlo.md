# Stochastic modelling (Monte Carlo)

Used for the probability-of-success view of a cashflow plan and for APTA where COBS 19.1.2CR allows
stochastic analysis provided the 50th percentile is no less conservative than the deterministic
Annex 4A/4C analysis (the engine asserts this and flags the run if it is not).

## Model
- Asset classes: UK equity, global equity, government bonds, corporate bonds, property, cash, with
  an expected nominal return μ_i, volatility σ_i and correlation matrix ρ on the assumption set
  (capital market assumptions, versioned with an as-at date).
- Annual portfolio return for path `p`, year `y`:
  `r = exp(Σ_i w_i·(μ_i − σ_i²/2) + (L·z)_i·σ_i) − 1` where `L` is the Cholesky factor of ρ and `z`
  is a vector of independent standard normals. Weights `w` come from the holdings' asset allocation
  (rebalanced annually).
- Inflation is stochastic with its own μ/σ and correlation to cash, or fixed when `FixedInflation`.
- RNG: xoshiro256** seeded from the request seed (default seed = analysis id hash), normals via
  the polar Box–Muller method. Identical seed ⇒ identical paths on every platform.
- Paths: default 1,000, maximum 10,000. Path returns are generated in `double`, converted to
  `decimal` at the boundary; all cashflow arithmetic then runs the deterministic year loop with the
  path's return applied instead of the assumed growth.

## Outputs
- Percentiles (5, 10, 25, 50, 75, 90, 95) of total assets and of net income per year (fan chart).
- Probability of success = share of paths with no shortfall before the plan end age.
- Median path summary, worst-decile shortfall age, and a `ConservativenessCheck` comparing the
  50th-percentile fund at retirement to the deterministic result.
