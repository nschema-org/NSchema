# Roadmap

Ideas and planned features for future NSchema releases. Right now, these aren't commitments or a prioritized backlog, just ideas for things I think contribute to the goal of "Terraform for databases".

---

## CLI interface

The biggest missing piece for the Terraform positioning. Right now every consumer writes their own hosting boilerplate. A first-class CLI would give teams a standard `nschema plan` / `nschema apply` / `nschema refresh` entry point without writing any code.

Related: the CLI is also the natural home for features like saved plan files, state inspection, and schema generation (see below).

---

## Saved plan files

Terraform's `plan -out=tfplan` + `apply tfplan` pattern gives a critical guarantee: what you reviewed in CI is exactly what gets applied in production, with no opportunity for the schema to drift between the two steps.

**Design sketch:**
- `IMigrationPlanSerializer` handles the format (versioned JSON, similar to `ISchemaStateSerializer`).
- A `IMigrationPlanStore` (or simple file/S3 path) controls where the plan is written and read from.
- During `Plan`: if a plan store is configured, serialize the `MigrationPlan` and write it out.
- During `Apply`: if a plan store is configured and a plan exists, load it and skip re-planning — go straight to compile and execute.
- The saved artifact is the `MigrationPlan` domain model (pre-SQL-compilation), not the raw SQL. This ensures custom `ISqlPlanner` overrides still run at apply time.

**Prerequisite already done:** `ICompiledMigration.Plan` was added in 2.0.0-alpha.1 so the pipeline has access to the domain plan at every stage.

**Key complexity:** `MigrationPlan` contains a `MigrationAction` class hierarchy that will need polymorphic JSON serialization (System.Text.Json `[JsonPolymorphic]` or a custom converter).

---

## State inspection

A way to view and reason about what NSchema thinks the current state of the database is — particularly useful during incidents or when debugging unexpected plan output.

**Design sketch:**
- At its simplest, a `nschema state show` CLI command that calls `ISchemaStateStore.Read()` and renders the result.
- More structured: a dedicated `Inspect` operation / `NSchemaApplication.Inspect()` that produces a diffable representation of the stored state.

No API changes required — `ISchemaStateStore.Read()` already returns the full `DatabaseSchema`.

---

## State locking

Prevents concurrent applies from overwriting each other's state. Currently last-write-wins, which is acceptable when deploys are serialised but unsafe otherwise.

**Design pattern (decided):** locking is a *separate opt-in interface* rather than methods on `ISchemaStateStore`. Stores that support locking implement `ISchemaStateLock` (name TBD); NSchema checks `if (store is ISchemaStateLock)` before acquiring. This keeps `ISchemaStateStore` implementable without locking support.

**Backend options:** S3 conditional writes (`If-None-Match` / `If-Match`), DynamoDB lock table (the Terraform pattern), or a database row lock for in-process scenarios.

---

## Import / bootstrap

`terraform import` equivalent. Currently `Refresh` captures the full live schema into state, which handles the initial bootstrap. A more targeted import would let teams selectively bring specific schemas or tables under NSchema management without capturing everything.

Useful when adopting NSchema on an existing database incrementally.

---

## Drift detection

Show how the live database has drifted from the last known state, without proposing any desired-state changes. Distinct from the current `Plan` operation (which diffs desired vs. current) — this is state vs. live.

Practically: `Plan` with `UseCurrentSchemaState()` already surfaces drift as unexpected changes in the plan output. A dedicated `Drift` or `Detect` mode would make the intent explicit and produce output clearly separated from "here's what I'm going to change."

Adds a value to `MigrationOperation` — a breaking change, so revisit when there's a concrete use case rather than pre-emptively.

---

## Multiple state backends

Currently: `FileSchemaStateStore` (built-in) and `S3SchemaStateStore` (NSchema.Aws). Teams on Azure or GCP hit a wall.

- `NSchema.Azure` — Azure Blob Storage backend
- `NSchema.Gcp` — Google Cloud Storage backend

Each would follow the same pattern as `NSchema.Aws`: an `ISchemaStateStore` implementation + `UseStateStore*` builder extension.

---

## Workspaces / environment scoping

First-class concept for managing state per environment (dev, staging, production). Terraform workspaces isolate state under a namespace.

For NSchema, convention-based key naming already works (`nschema/{env}/state.json`) and is probably sufficient. A workspace concept would add builder-level support so the environment name is injected automatically rather than being embedded in the key string.

---

## Structured plan output for CI

Machine-readable plan output (JSON or SARIF) that CI systems, PR comment bots, and Slack notifications can consume directly, without each consumer implementing a custom `IMigrationReporter`.

`IMigrationReporter.ReportPlan(MigrationPlan)` already receives the full domain plan, so a structured reporter is implementable today. The gap is a built-in, well-documented format that becomes a standard integration point.

---

## Schema generation from an existing database

The reverse flow: given a live database, emit the `AbstractSchemaProvider` C# code that describes it. Massively lowers the adoption barrier for teams with existing databases who don't want to write schema definitions from scratch.

Would likely live in the CLI and database-provider packages (e.g. `nschema generate --provider postgres`), using the existing `ISchemaProvider` introspection already implemented in `NSchema.Postgres`.
