# ADR-0007: Percentages in DTOs, fractions in the domain and engines

Date: 2026-09-07. Status: accepted.

**Context.** Advisers and provider charge sheets express rates as percentages (0.25% platform
charge); the engines need fractions (0.0025m) and must never mix the two.

**Decision.** Every DTO field that carries a rate ends in `Pct` and holds a percentage; every domain
and engine value is a fraction. Conversion happens only in `SwitchPoint.Application.Mapping.Pct`
and the mappers that use it; validators range-check percentages (0–100 unless negative rates are
meaningful); stored JSON columns therefore also hold percentages. Decimals are serialised with a
canonical scale (`NormalisedDecimalConverter`) so that 0.25 and 0.2500 hash identically.

**Consequences.** A wrong unit is a type-name mismatch that reviewers can spot (`OcfPct` vs `Ocf`);
tests assert the round trip (0.25 ↔ 0.0025m); the front end formats percentages without dividing.
