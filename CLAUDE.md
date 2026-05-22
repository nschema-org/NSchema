# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/NSchema.Tests

# Run Postgres integration tests (requires Docker)
dotnet test tests/NSchema.Postgres.Tests

# Run a single test
dotnet test tests/NSchema.Tests --filter "FullyQualifiedName~DefaultSchemaComparerTests"
```

## Architecture

NSchema is a declarative database schema migration library for .NET. The user describes the schema they want via `AbstractSchemaProvider`; NSchema introspects the database, diffs, and applies the difference.

The application is a hosted .NET app. `NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder` (wraps `HostApplicationBuilder`). `Build()` produces an `NSchemaApplication` (`IHost`). The migration itself runs as a `BackgroundService` — `NSchemaHost` (`src/NSchema/Hosting/NSchemaHost.cs`) — which calls the migration plan provider, the SQL planner, and the SQL executor, then signals `IHostApplicationLifetime` to stop.

### Pipeline

Orchestrated by `DefaultMigrationPlanProvider` (`src/NSchema/Migration/DefaultMigrationPlanProvider.cs`) followed by `NSchemaHost`:

1. **Collect desired state** — every `IDesiredSchemaProvider` is invoked; results merged by `ISchemaAggregator` into a single `DatabaseSchema`.
2. **Validate desired schema** — every `ISchemaPolicy` runs against the merged schema. Failures throw `PolicyViolationException`.
3. **Read current state** — `ICurrentSchemaProvider` (supplied by the database provider) queries the live database for the schemas in scope (declared schemas + `DropSchema` names).
4. **Diff** — `ISchemaComparer` (default `DefaultSchemaComparer`) produces a `MigrationPlan` of `MigrationAction` records (subclasses in `src/NSchema/Migration/Plan/`).
5. **Inject deployment scripts** — pre/post scripts from `IDeploymentScriptProvider`s are inserted as `RunPreDeploymentScript` / `RunPostDeploymentScript` actions at the ends of the plan.
6. **Transform plan** — `IMigrationPlanTransformer`s run in registration order. The built-in `ActionOrderingTransformer` sorts actions into a safe dependency order.
7. **Validate plan** — every `IMigrationPolicy` runs against the transformed plan. The built-in `DestructiveActionMigrationPolicy` enforces `MigrationOptions.DestructiveActionPolicy`.
8. **Plan SQL** — `ISqlPlanner` (provider-specific, e.g. `PostgresSqlPlanner`) converts the `MigrationPlan` into a `SqlPlan` of `SqlStatement`s.
9. **Execute** — `ISqlExecutor` (default `DefaultSqlExecutor`) runs the statements. If `MigrationOptions.DryRun` is true, the plan is logged instead.

### Project layout

| Project | Purpose |
|---|---|
| `src/NSchema` | Core abstractions, fluent schema builder, default pipeline implementations. |
| `src/NSchema.Postgres` | Postgres `ICurrentSchemaProvider` (`PostgresSchemaProvider`) and `ISqlPlanner` (`PostgresSqlPlanner`). Registered via `builder.UsePostgres(...)`. |
| `tests/NSchema.Tests` | Unit tests (xUnit, Shouldly, NSubstitute). |
| `tests/NSchema.Postgres.Tests` | Integration tests against a real Postgres container (Testcontainers). |
| `samples/NSchema.Sandbox` | Example console app. |
| `samples/NSchema.Sandbox.AppHost` | .NET Aspire host for the sandbox. |

### Defining a schema

Subclass `AbstractSchemaProvider` and call `Schema()` / `Table()` / `Column()` etc. via the fluent builders. The fluent API does not take nested lambdas — `Table(name)` returns a `TableBuilder` that's mutated directly.

```csharp
public class MySchema : AbstractSchemaProvider
{
    public MySchema()
    {
        var users = Schema("app").Table("users");
        users.Column("id", SqlType.Int).PrimaryKey("users_pkey");
        users.Column("name", SqlType.Text).NotNull();
    }
}
```

Providers are registered with `builder.AddSchema<T>()` or `builder.AddSchemasFromAssemblyContaining<T>()`.

### Extension points

| Interface | Registered via |
|---|---|
| `IDesiredSchemaProvider` | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()` |
| `ISchemaPolicy` | `AddSchemaPolicy<T>()` |
| `IMigrationPlanTransformer` | `AddPlanTransformer<T>()` |
| `IMigrationPolicy` | `AddMigrationPolicy<T>()` |
| `IDeploymentScriptProvider` | `AddScriptProvider<T>()` / `AddPre/PostDeploymentScriptFromFile(...)` / `AddPre/PostDeploymentScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor` | `UseSqlExecutor<T>()` (replaces default) |
| `ISchemaComparer`, `ISchemaAggregator`, `IMigrationPlanProvider` | Override via `Services.AddSingleton<...>()` before `Build()` (defaults registered with `TryAdd`) |
| `ICurrentSchemaProvider`, `ISqlPlanner` | Supplied by a database-provider extension (e.g. `UsePostgres(...)`) |

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Migration options (`MigrationOptions`)

- `DestructiveActionPolicy` — `Error` (default), `Warn`, or `Allow`. Applied by `DestructiveActionMigrationPolicy` to any action whose `IsDestructive` is true. Configured via `WithDestructiveActionPolicy(...)`.
- `DryRun` — when true, the plan is logged but not executed. Configured via `WithDryRun()`.
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`.
