# ADR-0005: Tamper-evident audit log as a per-firm SHA-256 hash chain

Date: 2026-09-06. Status: accepted.

**Context.** A suitability file must show that the analysis behind a recommendation was not altered
after the fact. Database-level immutability is not visible to a regulator reading an export.

**Decision.** Every audit event stores the hash of the previous event in the same firm's chain and its
own hash over the canonical JSON of its content. Reports embed the analysis hash. A verification
endpoint re-walks the chain and returns the first broken link.

**Consequences.** Append-only writes; a background verifier can alert on breaks; canonical JSON
serialisation must be stable across .NET versions (ordered properties, invariant culture).
