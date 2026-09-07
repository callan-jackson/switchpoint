# ADR-0006: Persist value objects as JSON columns mapped through the Application DTOs

Date: 2026-09-07. Status: accepted.

**Context.** Charge schedules, guarantees, holdings, DB tranches, plan assets and product charge
versions are immutable domain records with validating constructors and, in some cases, private
constructors and static factories (e.g. `Indexation`, `RevaluationRule`). System.Text.Json cannot
construct those directly, and relational tables for tiered bands would fragment a value that is
always read and written as a unit.

**Decision.** Store each value object as a JSON text column. The EF Core value converter maps the
domain value to the corresponding Application DTO (`ChargeScheduleDto`, `GuaranteesDto`, ...)
using the same bidirectional mappers the API uses, and back again on read. Change-tracking
comparers go through the same DTO so snapshots never touch private constructors. Private
collection fields (`_holdings`, `_tranches`, ...) are mapped as field-only properties.

**Consequences.** One serialisation contract for the API, persistence and reports; the DTO mappers
are the single place where the percent ↔ fraction rule lives (ADR-0007); JSON columns are opaque
to SQL filtering, which is acceptable because every query filters on scalar columns (firm, client,
ISIN). SQL Server and SQLite behave identically. Domain entities carry a private parameterless
constructor for EF materialisation, and get-only properties are mapped explicitly in
`SwitchPointDbContext` because EF Core does not map them by convention.
