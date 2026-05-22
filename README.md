# NSchema

A declarative database schema migration library for .NET.

You describe the schema you want in C#. NSchema reads the current state of the database, diffs it against your desired state, then generates and applies a migration plan to close the gap.

## Quick start

Install the core package and a provider for your database:

```bash
dotnet add package NSchema
dotnet add package NSchema.Postgres
```

Declare a schema by subclassing `AbstractSchemaProvider`:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        var users = Schema("app").Table("users");
        users.Column("id", SqlType.Text).PrimaryKey("users_pkey");
        users.Column("email", SqlType.Citext).NotNull();
        users.Column("name", SqlType.Text).NotNull();
        users.Index("uc_users_email", ["email"]).Unique();
    }
}
```

Wire up and run the application:

```csharp
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var app = builder.Build();
await app.RunAsync();
```

On startup, NSchema introspects the database, computes the difference between live state and the schemas you registered, and applies the resulting plan in a single transaction.

## Pipeline

Every run flows through the same pipeline. Each stage has an interface you can swap or extend.

1. **Collect desired state** — every registered `IDesiredSchemaProvider` is invoked and the results merged by `ISchemaAggregator` into a single `DatabaseSchema`.
2. **Validate the desired schema** — each `ISchemaPolicy` runs against the merged schema. Use these to enforce conventions (naming, required columns, banned types) before anything touches the database.
3. **Read current state** — `ICurrentSchemaProvider` (supplied by the database provider, e.g. `UsePostgres`) queries the live database for the schemas you've declared.
4. **Diff** — `ISchemaComparer` (default: `DefaultSchemaComparer`) produces a `MigrationPlan` of `MigrationAction`s. Renames are detected via the `OldName` property (set through `RenamedFrom(...)` on the fluent builders) on schemas, tables, and columns.
5. **Inject deployment scripts** — any pre-/post-deployment scripts contributed by `IDeploymentScriptProvider` implementations are prepended/appended to the plan as `RunPreDeploymentScript` / `RunPostDeploymentScript` actions.
6. **Transform the plan** — every `IMigrationPlanTransformer` runs in sequence. The built-in `ActionOrderingTransformer` topologically sorts actions so dependencies are respected (e.g. foreign keys dropped before their tables).
7. **Validate the plan** — each `IMigrationPolicy` inspects the final plan. The built-in `DestructiveActionMigrationPolicy` enforces `MigrationOptions.DestructiveActionPolicy` (`Error` | `Warn` | `Allow`).
8. **Plan SQL** — `ISqlPlanner` (supplied by the database provider) translates the `MigrationPlan` into a `SqlPlan` of database-specific statements.
9. **Execute** — `ISqlExecutor` runs the SQL plan against the database. By default, the whole plan runs in a single transaction; this is configurable via `MigrationOptions.TransactionMode`.

`WithDryRun()` runs the full pipeline up to execution and logs the plan without applying it.

## Extension points

Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                                    | Purpose                                                                        | Registered via                                                                                                                 |
|----------------------------------------------|--------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| `IDesiredSchemaProvider`                     | Contribute schemas to the desired state. Usually via `AbstractSchemaProvider`. | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                                                                   |
| `ISchemaPolicy`                              | Validate the merged desired schema.                                            | `AddSchemaPolicy<T>()`                                                                                                         |
| `IMigrationPlanTransformer`                  | Rewrite or reorder the generated plan.                                         | `AddPlanTransformer<T>()`                                                                                                      |
| `IMigrationPolicy`                           | Validate the final plan before execution.                                      | `AddMigrationPolicy<T>()`                                                                                                      |
| `IDeploymentScriptProvider`                  | Supply raw SQL to run pre- or post-deployment.                                 | `AddScriptProvider<T>()`, `AddPre/PostDeploymentScriptFromFile(...)`, `AddPre/PostDeploymentScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor`                               | Override how SQL is sent to the database (e.g. logging, custom transactions).  | `UseSqlExecutor<T>()`                                                                                                          |
| `ISchemaComparer`                            | Replace the diff algorithm.                                                    | `Services.AddSingleton<ISchemaComparer, T>()`                                                                                  |
| `ICurrentSchemaProvider` / `ISqlPlanner`     | Add support for another database.                                              | Provider package (e.g. `UsePostgres(...)`)                                                                                     |

## Renaming

Renames are explicit. Call `RenamedFrom(...)` on a schema, table, or column so the comparer can match it to the existing one instead of dropping and recreating:

```csharp
var accounts = Schema("app").Table("accounts").RenamedFrom("users");
accounts.Column("display_name", SqlType.Text).RenamedFrom("name");
```

## Project layout

| Project                           | Purpose                                                               |
|-----------------------------------|-----------------------------------------------------------------------|
| `src/NSchema`                     | Core abstractions, fluent schema builder, default pipeline.           |
| `src/NSchema.Postgres`            | Postgres `ICurrentSchemaProvider` and `ISchemaMigrator`.              |
| `tests/NSchema.Tests`             | Unit tests (xUnit, Shouldly, NSubstitute).                            |
| `tests/NSchema.Postgres.Tests`    | Integration tests against a real Postgres container (Testcontainers). |
| `samples/NSchema.Sandbox`         | Example console app showing schema declarations and script providers. |
| `samples/NSchema.Sandbox.AppHost` | .NET Aspire host for the sandbox.                                     |

## Building and testing

```bash
dotnet build
dotnet test                                 # all tests
dotnet test tests/NSchema.Tests             # unit tests only
dotnet test tests/NSchema.Postgres.Tests    # integration tests (requires Docker)
```

## License

See [LICENSE](LICENSE).
