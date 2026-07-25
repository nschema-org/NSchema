# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project

- NSchema is a declarative schema management tool for relational databases. Think Terraform, but with extra features to facilitate data migration.
- It's primarily consumed through its CLI, a dotnet global tool called `NSchema`, although its engine is published as a standalone nuget package called `NSchema.Core` (that's this project).
- The CLI project, the database providers, and the state providers are all published as NuGet packages from their own repos which should be checked out alongside this one.

## Instructions

While working in this repo, follow these instructions:

- Never commit. I will review your changes and stage/commit them as I see fit.
- Be brief. For code comments, and XML docs, one short sentence is enough.
- Public facing changes should be included in CHANGELOG.md.
- When updating the changelog, changes are relative to the _previous released version_, not the current branch/PR/commit.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test class
dotnet test tests/NSchema.Core.Tests --filter "FullyQualifiedName~DefaultDatabaseComparerTests"

# Run a single test method
dotnet test tests/NSchema.Core.Tests --filter "FullyQualifiedName~DefaultDatabaseComparerTests.Compare_AddsTable"
```

### Tests

This project:
- Uses a combination of unit, integration tests, and e2e tests, as outlined by the testing pyramid.
- We also do snapshot testing for output surfaces like the diff renderer, pplan renderer, etc. using **Verify**.
- Tests should be arranged in an Arrange/Act/Assert structure with heading comments: (// Arrange, // Act, // Assert)
- Testing framework is **xUnit V3**.
- Mocks use **NSubstitute**.
- Assertions use **Shouldly**.
- Integration tests are done using Testcontainers.

## NSQL

The project language for NSchema is SQL-flavored, so users can write familiar `CREATE TABLE` statements.

### Configuration

NSchema has a small configuration grammar for configuring plugins, the database, the state backend, and the engine using `PLUGIN`, `STATE`, `BACKEND` and `ENGINE` blocks.

### Directives

For actions outside regular SQL, the language has directives like `TEMPLATE`, `RENAME` or `SCRIPT`. These describe mutations to the desired project structure by renaming objects, attaching migration scripts, or allowing reusable table/column statements.

## Architecture

NSchema borrows the .NET hosting model, exposing its builder through `NSchemaApplication.CreateBuilder()`, which exposes a set of configuration methods.

The `Build()` function produces a disposable `NSchemaApplication` object, that exposes the public surface for feature areas like locking and state, but also provides commonly-orchestrated actions like plan and apply.

Unlike a common application host, there is no `BackgroundService` and no host lifecycle. The app instance is reusable and may be invoked multiple times across its lifetime.

### Conventions

- Wherever possible, follow the result pattern and return Result<T> instead of throwing exceptions.
- Prefer value objects over raw primitives, especially when custom behavior or equality are involved.
- Prefer behavior to live on its owning domain model rather than as helper functions or classes.

### Layers

- The codebase is feature sliced, with each slice further sub-divided into a **domain layer**, an **application layer**.
- Some top-level feature slices are dependent on others, for example `Plan` depends on `Diff`.

- **Domain layer**, one namespace per pipeline stage:
  - `Project/` — the schema model (`Project/Domain/Models/`, rooted at `Database` holds all info about the _desired_ project state.
  - `Diff/` — the structured diff model (`Diff/Domain/Models/`, rooted at `DatabaseDiff`. — **the complete current→desired difference**:
  - `Plan/` — the plan model (`Plan/Domain/Models/`, rooted at `MigrationPlan`. Holds the complete diff and the SQL statements that execute it).
  - `Sql/` — `SqlDialect` (the abstract per-action rendering base a provider subclasses — one overridable method per migration action, and `ISqlExecutor`. Core ships no dialect.
- **Application layer:**
  - `Operations/` — one vertical slice per operation (see below).
  - `IMigrationWorkflow` (`Operations/Services/`) — the imperative shell operations share.

### Operations

Each operation is its own vertical slice: handlers are registered as `IOperation<{Name}Arguments, Result<{Name}Result>>` and invoked through the public `INSchemaOperations` facade (`app.Operations`). Operations narrate transient progress through `IProgress<OperationProgress>` and leave rendering, locking, and confirmation to the caller.

Built-in operations (`src/NSchema.Core/Operations/`):

- **`PlanOperation`** (`Plan`) — computes a plan without executing. A plan always diffs recorded state against the target. Teardowns are produced by comparing against `Empty`. The plan carries `MigrationPlan.Managed`.
- **`ApplyOperation`** (`Apply`) — executes a migration plan against a given database. If a plan file is not supplied, it will generate a fresh one. Applying always re-runs any policies for safety.
- **`RefreshOperation`** — captures the live schema to the state store without planning or applying.
- **`ValidateOperation`** — loads the project and validates it against registered `IProjectPolicy` implementations; no planning/applying.
- **`DriftOperation`** — reads recorded (offline) state and live (online) schema and compares (direction recorded → live, so an out-of-band add reads as `Add`).
- **`ImportOperation`** — fetches the live schema (optionally restricted by `Scope`) and writes it under `OutputDirectory` as SQL DDL via `DdlWriter`.
- **`DoctorOperation`** — runs read-only health checks against the configured infrastructure and reports the outcome of each.

**`IMigrationWorkflow`** (`Operations/Services/`) is the imperative shell around the pure planner shared by `Plan`, `Apply`, `Refresh`, and `Validate`: `ComputePlan(target, scope)` resolves the desired side (the project, or nothing for a teardown) and the recorded current side, then runs `IMigrationPlanner` (returning `Result<MigrationPlan>`), `Validate()` runs just the project policies, and `Refresh()` captures the live schema to the store, returning `Result<StateCapture>` (a failure when no store is configured). `Drift`, `Import`, and `Doctor` do **not** use the workflow — they talk to `IDatabaseProvider` / `IDatabaseComparer` / the importer directly. The workflow does not lock, confirm, or render.

### Database

- Database providers are plugins.

### State

- Like Terraform, NSchema tracks the managed state in a backend store.
- Unlike Terraform, the state includes a snapshot of the entire database schema, along with an index of which objects are managed. This is so that migrations don't cause unexpected errors due to keys or indexes not included in the original plan.
- The state also stores a list of which migration scripts have already been run, so they remain idempotent
- The state store supports locking via the `IStateLock` interface, accessible through `IStateLockManager` by consumers.
- Locking is expected to be done manually by the consumer, where needed.

### Planner pipeline

`MigrationPlanner` (`Plan/Domain/MigrationPlanner.cs`) conducts the pipeline, producing `Result<MigrationPlan>` — the complete artifact — with any `PolicyDiagnostic`s riding on the `Result`. **A policy block is a failure that still carries the complete plan** ("may not apply", not "stopped computing"): every stage runs regardless, and error severity is what blocks application — the CLI decides how to act. **The line between the stages: Diff answers "what is different" — schema changes and the script runs they imply; Plan answers "how it executes" — order and SQL.** Stages:

1. **Project stage** — runs every `IProjectPolicy` against the declared `ProjectDefinition` (schema plus scripts). Also exposed standalone as `IMigrationPlanner.Validate(desired)` (used by the validate operation).
2. **Diff stage** — `IProjectComparer` produces the complete `DatabaseDiff`: `IDatabaseComparer` emits the structural tree directly (no flat-action intermediate), run-once resolution drops already-executed **deployment** declarations (their skip warnings are diff-stage diagnostics; change-event scripts have no ledger and pass through, gated by the compare), and the matcher inlines each change-event script on the node it prepares while the deployment scripts ride the diff's root `DeploymentScripts` list (an unmatched change-event script is a dead-migration info).
3. **Linearize stage** — `IPlanLinearizer` (default `PlanLinearizer`) walks the diff and emits `MigrationAction`s in a safe dependency order, weaving the diff's own scripts in as `ExecuteScript` actions: each change node's inlined `MigrationScript` splices at that change, the root's deployment scripts bookend the list (pre first, post last).
4. **Render stage** — the planner walks the actions in order and renders each through the required `SqlDialect` (one action in, one or more statements out — decomposition, never reordering), scripts included: an `ExecuteScript` typically renders as its verbatim `Statement` (the action carries that default), but a dialect may validate or normalize the SQL. A rendering's diagnostics ride the plan result — an action the dialect doesn't support is an error that blocks application, not an exception. The artifact is the diff plus the statements; the actions themselves are discarded.
5. **Plan stage** — runs every `IPlanPolicy` against the complete artifact, post-render, so policies see exactly what an apply would execute (the diff, its scripts, and the statements) — a matched backfill legitimately suppresses the data-hazard warning because script coverage rides the plan's diff. The built-ins run here: `DestructiveActionPolicy` (enforcing `DestructiveActionOptions.Policy`), `DataHazardPolicy`, and `EnumValueRemovalPolicy`. Apply re-runs the same policies against the plan it is given.

## When writing documentation and XML/code comments.

- Public facing changes should be included in CHANGELOG.md.
- Don't overdocument everything. A brief, single-line summary is enough for XML docs. In some advanced cases where explanation is actually needed, then add another sentence or two in a Remarks block.
- When writing code comments, be aware that repeating the same reasoning in multiple places creates coupling and very quickly becomes out of date, so keep comments restricted to what actually matters at the place they're written.
- Don't expose implementation details in documentation or changelogs, as documentation is a form of contract, so changing an implementation becomes a breaking change.
- Stop confirming negatives. If I ask you to change something like "you've accidentally coupled these two services", don't then go and add five comments in various places saying "these two classes aren't coupled", because nobody was expecting them to be in the first place.
