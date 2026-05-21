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

NSchema is a declarative database schema migration library for .NET. The core migration pipeline runs in `DefaultNSchemaRunner` (`src/NSchema/Hosting/DefaultNSchemaRunner.cs`) and follows this sequence:

1. **Collect desired state** — all registered `IDesiredSchemaProvider` implementations are called and their results merged by `ISchemaAggregator`.
2. **Validate schema** — `ISchemaPolicy` implementations run against the merged `DatabaseSchema`.
3. **Fetch current state** — `ICurrentSchemaProvider` queries the live database for schemas in scope.
4. **Diff** — `ISchemaComparer` (default: `DefaultSchemaComparer`) produces a `MigrationPlan` containing a list of `SchemaAction` objects.
5. **Transform plan** — `IMigrationPlanTransformer` implementations run in sequence. The built-in `ActionOrderingTransformer` sorts actions into the correct dependency order (e.g. drop FKs before dropping tables).
6. **Validate actions** — `IActionPolicy` implementations run against the transformed plan. The built-in `DestructiveActionPolicyEnforcer` enforces `MigrationOptions.DestructiveActionPolicy` (default: `Error`).
7. **Migrate** — `ISchemaMigrator` executes the plan against the database.

### Project layout

| Project | Purpose |
|---|---|
| `src/NSchema` | Core abstractions and default implementations |
| `src/NSchema.Postgres` | Postgres `ICurrentSchemaProvider` and `ISchemaMigrator` |
| `tests/NSchema.Tests` | Unit tests (xUnit, Shouldly, NSubstitute) |
| `tests/NSchema.Postgres.Tests` | Integration tests against a real Postgres container (Testcontainers) |
| `samples/NSchema.Sandbox` | Example console app |
| `samples/NSchema.Sandbox.AppHost` | .NET Aspire host for the sandbox |

### Defining a schema

Users subclass `AbstractSchemaProvider` and call `Schema()` / `Table()` / `Column()` / etc. via the fluent builder API. The provider is registered with `builder.AddSchema<T>()`.

```csharp
public class MySchema : AbstractSchemaProvider
{
    public MySchema()
    {
        Schema("app")
            .Table("users", t => t
                .Column("id", SqlType.Int)
                .Column("name", SqlType.Text));
    }
}
```

### Extension points

| Interface | Registered via |
|---|---|
| `IDesiredSchemaProvider` | `builder.AddSchema<T>()` |
| `ISchemaPolicy` | `builder.AddValidationPolicy<T>()` |
| `IMigrationPlanTransformer` | `builder.AddPlanTransformer<T>()` |
| `IActionPolicy` | `builder.AddMigrationActionPolicy<T>()` |

### Renaming

Schemas, tables, and columns support rename detection via a `PreviousName` property on the domain model. The comparer matches on `PreviousName` when the current name is not found.

### Destructive action policy

Controlled via `MigrationOptions.DestructiveActionPolicy`: `Error` (default), `Warn`, or `Allow`. Any `SchemaAction` with `IsDestructive = true` is subject to this policy.
