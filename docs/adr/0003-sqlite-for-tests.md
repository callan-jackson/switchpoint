# ADR-0003: SQL Server in production, SQLite for tests and local runs

Date: 2026-09-06. Status: accepted.

**Context.** The production store is Azure SQL. Developers and CI should not need Docker or a SQL
Server instance to run the suite, and the local machine that builds this project has limited disk.

**Decision.** EF Core with a provider switch (`Database:Provider = SqlServer | Sqlite`). Complex
value objects (charge schedules, assumption sets) are stored as JSON columns, which both providers
support. A nightly CI job runs the integration suite against a SQL Server container to catch
provider-specific drift.

**Consequences.** Migrations are generated for SQL Server; SQLite uses `EnsureCreated` from the model.
Query features unsupported by SQLite (e.g. `OPENJSON` filters) are avoided or guarded.
