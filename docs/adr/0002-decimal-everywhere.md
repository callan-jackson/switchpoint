# ADR-0002: `decimal` for all monetary and rate arithmetic

Date: 2026-09-06. Status: accepted.

**Context.** Critical yields and RIYs are reported to 0.1% and must be reproducible to the penny
across runs, machines and years; binary floating point introduces drift that compounds over 480
monthly steps and is hard to reason about in a compliance review.

**Decision.** `decimal` (28–29 significant digits) is the only numeric type on money and rate paths.
Transcendental functions are provided by `DecimalMath` (Taylor/Newton to 1e-20). `double` is
permitted only inside the pseudo-random generator; its output is converted before any arithmetic
with money.

**Consequences.** Roughly an order of magnitude slower than `double`; still far inside the
performance budget (a 40-year monthly projection is ~500 multiplications). Property tests assert
determinism and monotonicity rather than tolerances where possible.
