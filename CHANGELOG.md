# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0-alpha.1] - 2026-05-29

While version 1.0.0 was useful, the pipeline model wasn't quite flexible enough. I've taken the opportunity to rethink the abstractions around applying migrations.

Additionally, I've expanded the concept of a "DryRun" into a more explicit Plan/Apply model, which can be accessed both through by configuration and explicit methods. This should reduce the chance of running the wrong operation.

### Added

- Replaced the `MigrationOptions.DryRun` flag with a `MigrationOperation` enum and `MigrationOptions.Operation` option to select what a run does (`Plan` or `Apply`), configurable via `RunOperation(...)`. It defaults to `Plan` if not set, so changes cannot be applied unintentionally.
- Explicit `NSchemaApplication.Plan(...)` and `Apply(...)` entry points that run a specific operation. This overrides any pre-configured `Operation` for that run. (`RunAsync()` continues to use the configured operation.)
- `IMigrationCompiler` and `ICompiledMigration`: a migration plan is compiled into an inspectable, executable unit of work — `Preview` (what would happen) plus `Execute` (perform it). Register a custom compiler via `UseMigrationCompiler<T>()`.

### Changed

- The pipeline now compiles the plan into an `ICompiledMigration` and previews it separately from execution. The same compiled unit is both previewed and executed, so the preview always matches what runs, and the plan/apply distinction no longer leaks into executor implementations. Compiled migration reporting moved into the pipeline.

### Removed

- **Breaking:** `MigrationOptions.DryRun` and `DryRunOnly()` — use `Operation` / `RunOperation(MigrationOperation.Plan)` instead.
- **Breaking:** `IMigrationExecutor` and `UseMigrationExecutor<T>()` — implement `IMigrationCompiler` (returning an `ICompiledMigration`) and register it with `UseMigrationCompiler<T>()`.

## [1.0.1] - 2026-05-28

### Fixed

- Fixed a bug where primary keys, foreign keys, and indexes weren't being displayed in the plan UI for new tables.

## [1.0.0] - 2026-05-27

First stable release. The public API is now covered by semantic versioning — breaking changes will only ship in a new major version.

### Added

- Declarative schema definition via `AbstractSchemaProvider` and the fluent `Schema` / `Table` / `Column` / `Index` / `ForeignKey` builders.
- Hosted application model: `NSchemaApplication.CreateBuilder(...)` produces an `IHost`-backed app that runs the migration as a `BackgroundService`.
- A single `ISchemaProvider` interface used for both desired-state and current-state schemas, with the role determined at DI registration time (`AddSchema<T>()` / `UseSchemaSource<T>()`).
- Pluggable pipeline with extension points for every stage: `ISchemaPolicy`, `IMigrationPlanTransformer`, `IMigrationPolicy`, `IScriptProvider`, `ISqlPlanner`, `ISqlExecutor`, `IMigrationExecutor`.
- Rename detection for schemas, tables, and columns via `RenamedFrom(...)`.
- Migration options: `DestructiveActionPolicy` (`Error` / `Warn` / `Allow`), `DryRun`, `TransactionMode` (`Single` / `None`) with per-statement `RunOutsideTransaction` carve-outs, and `SchemaNames` scope filter via `ForSchemas(...)`.
- Built-in `ActionOrderingTransformer` (topological ordering of plan actions) and `DestructiveActionMigrationPolicy`.
- Pre- and post-deployment script support via `IScriptProvider`, `AddScriptFromFile(...)`, and `AddScriptsFromEmbeddedResources(...)`.
- SourceLink and symbol packages (`.snupkg`) published alongside the main package for source-level debugging.
