# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0-alpha.2]

Version 2 is focusing on improving the developer experience, with a more explicit but flexible model for planning and applying changes. The public API has been restructured to support this, and some breaking changes have been made to increase clarity and make operations more explicit.

V2 also introduces an optional Terraform-style "backend state store" so that plans can be made against state snapshots rather than the current live state.

### Added

- Replaced the `MigrationOptions.DryRun` flag with a `MigrationOperation` enum and `MigrationOptions.Operation` option to select what a run does (`Plan` or `Apply`), configurable via `RunOperation(...)`.
- Explicit `NSchemaApplication.Plan(...)` and `Apply(...)` entry points that run a specific operation. This overrides any pre-configured `Operation` for that run. (`RunAsync()` still uses the configured operation.)
- Added `IMigrationCompiler` and `ICompiledMigration`. Replaces `IMigrationExecutor` by compiling a migration plan into an executable unit of work. Register a custom compiler via `UseMigrationCompiler<T>()`.
- Added `ISchemaStateSerializer` for versioned serialization of schema state, with a built-in `DefaultSchemaStateSerializer`.
- Added `ISchemaStateStore` for optional backend state storage, and `UseStateStore<T>()` for registration. This allows plans to be generated against the last applied state rather than the current live state.
- Added `FileSchemaStateStore` implementation of `ISchemaStateStore` that saves the last applied schema to a local file. This is useful for simple scenarios or as a reference implementation for custom stores.
- Added control over where the current schema is read from: `UseCurrentSchemaState()` reads it from the state store, while `UseCurrentSchemaAuto()` reads from the store when planning and from the live database when applying.
- After a successful apply, NSchema now captures the resulting schema to the state store (when one is configured). The new `Refresh` operation (`MigrationOperation.Refresh` / `NSchemaApplication.Refresh()`) captures it without planning or applying.

### Changed

- The pipeline now compiles the plan into an `ICompiledMigration` and previews it separately from execution.
- **Breaking:** NSchema applications now default to `Plan` mode, so they won't apply changes unless explicitly configured or invoked with `Apply()`.
- **Breaking:** `IMigrationReporter` has moved to the `NSchema.Migration` namespace (was `NSchema.Hosting`).
- **Breaking:** `IMigrationReporter` is now a presenter responsible for displaying the plan and execution results, so it can be more easily customized.
- **Breaking:** `PolicyError` has a new `Severity` property. The existing 2-argument constructor still compiles, but custom `IMigrationPolicy` implementations should now signal warnings with `PolicySeverity.Warning`.

### Removed

- **Breaking:** `MigrationOptions.DryRun` and `DryRunOnly()` are gone. Use `Operation` / `RunOperation(MigrationOperation.Plan)` instead.
- **Breaking:** `IMigrationExecutor` and `UseMigrationExecutor<T>()` are gone. Implement `IMigrationCompiler` (returning an `ICompiledMigration`) and register it with `UseMigrationCompiler<T>()`.

## [1.0.1] - 2026-05-28

### Fixed

- Fixed a bug where primary keys, foreign keys, and indexes weren't being displayed in the reported plan for new tables.

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
