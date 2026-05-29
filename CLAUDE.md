# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test class
dotnet test tests/NSchema.Tests --filter "FullyQualifiedName~DefaultSchemaComparerTests"
```

Note: the Postgres provider was extracted to its own repository (see commit `dd3e695`). The solution (`NSchema.slnx`) contains only `src/NSchema` and `tests/NSchema.Tests`, where all real code lives.

## Architecture

NSchema is a declarative database schema migration library for .NET. The user describes the schema they want via `AbstractSchemaProvider`; NSchema introspects the database, diffs, and applies the difference.

The application is a hosted .NET app. `NSchemaApplication.CreateBuilder()` returns an `NSchemaApplicationBuilder` (wraps `HostApplicationBuilder`). `Build()` produces an `NSchemaApplication` (`IHost`). The migration itself runs as a `BackgroundService` — `NSchemaHost` (`src/NSchema/Hosting/NSchemaHost.cs`) — which dispatches to `IMigrationPipeline.Plan` or `IMigrationPipeline.Apply` based on `MigrationOptions.Operation`, then signals `IHostApplicationLifetime` to stop. The host itself contains no migration logic; orchestration lives in `DefaultMigrationPipeline` (`src/NSchema/Hosting/DefaultMigrationPipeline.cs`), which orchestrates user-facing reporting (handing artifacts to `IMigrationReporter`, a presenter) and calls `IMigrationPlanner` (to build the plan) then `IMigrationCompiler` (to compile the plan into an `ICompiledMigration` it previews and — for an apply — executes).

### Schema providers

A single `ISchemaProvider` interface (`src/NSchema/Migration/ISchemaProvider.cs`) is used for both desired-state and current-state schemas:

```csharp
Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
```

The role distinction lives at DI registration time, not on the type:

- **Desired** providers are registered as an enumerable `ISchemaProvider` (via `AddSchema<T>()` or assembly scanning).
- The **current** provider fills a single keyed slot under `ISchemaProvider.CurrentSchemaProviderKey` (`"NSchema.Current"`), registered via `UseCurrentSchema<T>()`. `DefaultMigrationPlanner` resolves it with `[FromKeyedServices(...)]`.

Keyed and unkeyed registrations are separate stores in MS DI, so the same implementation can be registered on both sides without bleeding.

The `schemaNames` parameter is a scope filter: `null` / empty means "return everything you describe"; otherwise the provider should restrict its result to those schemas.

### Pipeline

`DefaultMigrationPipeline` (`src/NSchema/Hosting/DefaultMigrationPipeline.cs`) runs the planner (steps 1–8) and then compiles and (for an apply) runs the result (steps 9–10). It orchestrates user-facing reporting via `IMigrationReporter`; the planner is pure and signals policy failures via `PolicyViolationException`, which the pipeline catches to surface to the user. `Plan` and `Apply` share a private `Prepare` step (plan → report diff → compile → report preview); `Apply` then executes the compiled migration.

Steps 1–8 run inside `DefaultMigrationPlanner` (`src/NSchema/Migration/DefaultMigrationPlanner.cs`):

1. **Resolve scope** — `MigrationOptions.SchemaNames` (set via `ForSchemas(...)`) is the authoritative scope. When unset, scope is derived after step 2 from the aggregated declared (and dropped) schemas.
2. **Collect desired state** — every desired `ISchemaProvider` is invoked with the scope; results merged by `ISchemaAggregator` into a single `DatabaseSchema`. Providers are expected to honor the scope filter themselves; the planner does not re-filter post-aggregation.
3. **Validate desired schema** — every `ISchemaPolicy` runs against the merged schema. Failures throw `PolicyViolationException`.
4. **Read current state** — the keyed `ISchemaProvider` queries the live database for the schemas in scope (explicit `SchemaNames`, or — when unset — declared schemas + `DropSchema` names from the aggregate).
5. **Diff** — `ISchemaComparer` (default `DefaultSchemaComparer`) produces a `MigrationPlan` of `MigrationAction` records (subclasses in `src/NSchema/Migration/Plan/`).
6. **Inject deployment scripts** — pre/post scripts from `IScriptProvider`s are inserted as `RunScript` actions at the ends of the plan.
7. **Transform plan** — `IMigrationPlanTransformer`s run in registration order. The built-in `ActionOrderingTransformer` sorts actions into a safe dependency order.
8. **Validate plan** — every `IMigrationPolicy` runs against the transformed plan. The built-in `DestructiveActionMigrationPolicy` enforces `MigrationOptions.DestructiveActionPolicy`.

Steps 9–10 run back in the pipeline via the registered `IMigrationCompiler` (default `SqlMigrationCompiler`). A `Plan` stops after the preview (step 9); an `Apply` continues into execution (step 10):

9. **Compile** — `IMigrationCompiler.Compile` turns the `MigrationPlan` into an `ICompiledMigration`: an inspectable unit exposing a `Preview` (the statements it would run) and an `Execute`. The default SQL compiler uses `ISqlPlanner` (provider-specific, supplied by a database-provider extension) to translate the plan into a `SqlPlan` of `SqlStatement`s, wrapped as a `CompiledSqlMigration`. The pipeline hands `Preview` to `IMigrationReporter.ReportPreview`. Compiling has no side effects.
10. **Execute** — for an `Apply`, `ICompiledMigration.Execute` performs the work; the SQL implementation runs the statements via `ISqlExecutor` (default `DefaultSqlExecutor`). For a `Plan`, nothing is executed.

### Defining a schema

Subclass `AbstractSchemaProvider` and call `Schema()` / `Table()` / `Column()` etc. via the fluent builders. The fluent API supports both a return style and a delegate style — `Table(name)` returns a `TableBuilder` that's mutated directly:

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

| Interface                                                        | Registered via                                                                                                                                              |
|------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)                                      | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                                                                                                |
| `ISchemaProvider` (current)                                      | `UseCurrentSchema<T>()` — keyed by `ISchemaProvider.CurrentSchemaProviderKey`; typically called from a database-provider extension (e.g. `UsePostgres(...)`) |
| `ISchemaPolicy`                                                  | `AddSchemaPolicy<T>()`                                                                                                                                      |
| `IMigrationPlanTransformer`                                      | `AddPlanTransformer<T>()`                                                                                                                                   |
| `IMigrationPolicy`                                               | `AddMigrationPolicy<T>()`                                                                                                                                   |
| `IScriptProvider`                                                | `AddScriptProvider<T>()` / `AddScriptFromFile(...)` / `AddScriptsFromEmbeddedResources(...)`                                                                |
| `ISqlExecutor`                                                   | `UseSqlExecutor<T>()` (replaces default)                                                                                                                    |
| `IMigrationCompiler`                                            | `UseMigrationCompiler<T>()` (replaces default `SqlMigrationCompiler`)                                                                                       |
| `ISchemaComparer`, `ISchemaAggregator`, `IMigrationPlanner`, `IMigrationPipeline`, `IMigrationReporter`, `IMigrationPlanRenderer` | Override via `Services.AddSingleton<...>()` before `Build()` (defaults registered with `TryAdd`)                     |
| `ISqlPlanner`                                                    | Supplied by a database-provider extension                                                                                                                   |

### Renaming

Schemas, tables, and columns support rename detection via the fluent `RenamedFrom(oldName)` method, which sets the `OldName` property on the domain model. The comparer matches on `OldName` when the current name isn't found.

### Migration options (`MigrationOptions`)

- `DestructiveActionPolicy` — `Error` (default), `Warn`, or `Allow`. Applied by `DestructiveActionMigrationPolicy` to any action whose `IsDestructive` is true. Configured via `WithDestructiveActionPolicy(...)`.
- `Operation` — `Plan` (default; compute and report the plan and its SQL preview without applying) or `Apply` (compute, then apply). Configured via `RunOperation(...)`, or overridden for a single run by calling `NSchemaApplication.Plan()` / `Apply()` (these mutate `Operation` then run the full host lifecycle; `RunAsync()` uses the configured value).
- `TransactionMode` — `Single` (default; whole plan in one transaction, with carve-outs for statements marked `RunOutsideTransaction`) or `None`.
- `SchemaNames` — optional `string[]` scope filter. When set, only these schemas are read from the live database, validated, and diffed; declarations or drops outside this set are ignored. When unset, scope is the union of every schema declared (or dropped) by the registered desired providers. Configured via `ForSchemas(...)`.
