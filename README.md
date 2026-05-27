# NSchema

A declarative database schema migration library for .NET.

You describe the schema you want in C#. NSchema reads the current state of the database, diffs it against your desired state, then generates and applies a migration plan to close the gap.

## Getting started

Install the core package and a database provider:

```bash
dotnet add package NSchema
dotnet add package NSchema.Postgres   # or another provider
```

Declare a schema by subclassing `AbstractSchemaProvider` using your preferred style:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        // Return style
        var users = Schema("app").Table("users");
        users.Column("id", SqlType.Text).PrimaryKey("users_pkey");
        users.Column("email", SqlType.Text).NotNull();
        users.Column("name", SqlType.Text).NotNull();
        users.Index("uc_users_email", ["email"]).Unique();

        // Delegate style
        Schema("app", s => s
            .Table("users", t => t
                .Column("id", SqlType.Text, c => c.PrimaryKey("users_pkey"))
                .Column("email", SqlType.Text, c => c.NotNull())
                .Column("name", SqlType.Text, c => c.NotNull())
                .Index("uc_users_email", ["email"], i => i.Unique())
            )
        );
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

## Schema providers

A single `ISchemaProvider` interface describes both the **desired** schema (what you want) and the **current** schema (what the database has). The role is determined at DI registration time, not on the type — the same implementation (for example a live Postgres reader) can be plugged into either side.

- **Desired** providers — registered via `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`. Multiple are aggregated.
- **Current** provider — a single instance, registered via `UseSchemaSource<T>()`. Database-provider extensions (e.g. `UsePostgres(...)`) call this for you.

`ISchemaProvider.GetSchema(schemaNames, ct)` takes an optional scope filter; when `null` (or empty), the provider returns its full schema, which makes a Postgres reader usable as either an export source or a live-state source without changes.

## Pipeline

Every run flows through the same pipeline. Each stage has an interface you can swap or extend.

1. **Resolve scope** — `MigrationOptions.SchemaNames` (set via `ForSchemas(...)`) is the authoritative scope for the run. When unset, scope is the union of every schema declared (or dropped) by the registered desired providers.
2. **Collect desired state** — every desired `ISchemaProvider` is invoked with the scope; results are merged by `ISchemaAggregator` into a single `DatabaseSchema`.
3. **Validate the desired schema** — each `ISchemaPolicy` runs against the merged schema. Use these to enforce conventions (naming, required columns, banned types) before anything touches the database.
4. **Read current state** — the current `ISchemaProvider` (supplied by the database provider, e.g. `UsePostgres`) queries the live database for the schemas in scope.
5. **Diff** — `ISchemaComparer` (default: `DefaultSchemaComparer`) produces a `MigrationPlan` of `MigrationAction`s. Renames are detected via the `OldName` property (set through `RenamedFrom(...)` on the fluent builders) on schemas, tables, and columns.
6. **Inject deployment scripts** — any pre-/post-deployment scripts contributed by `IScriptProvider` implementations are prepended/appended to the plan as `RunScript` actions.
7. **Transform the plan** — every `IMigrationPlanTransformer` runs in sequence. The built-in `ActionOrderingTransformer` topologically sorts actions so dependencies are respected (e.g. foreign keys dropped before their tables).
8. **Validate the plan** — each `IMigrationPolicy` inspects the final plan. The built-in `DestructiveActionMigrationPolicy` enforces `MigrationOptions.DestructiveActionPolicy` (`Error` | `Warn` | `Allow`).
9. **Plan SQL** — `ISqlPlanner` (supplied by the database provider) translates the `MigrationPlan` into a `SqlPlan` of database-specific statements.
10. **Execute** — `ISqlExecutor` runs the SQL plan against the database. By default, the whole plan runs in a single transaction; this is configurable via `MigrationOptions.TransactionMode`.

`DryRunOnly()` runs the full pipeline up to execution and logs the plan without applying it.

## Scoping to specific schemas

Set `MigrationOptions.SchemaNames` (via `ForSchemas(...)`) to scope a run to a subset of schemas. Useful for deploying schemas independently of one another, or for tooling that targets a single schema:

```csharp
builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .ForSchemas("app");   // only "app" is read, validated, and diffed
```

Declarations or drops for schemas outside the scope are ignored, so unmanaged schemas in the database are never touched.

## Extension points

Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                                   | Purpose                                                                        | Registered via                                                                             |
|---------------------------------------------|--------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired)                 | Contribute schemas to the desired state. Usually via `AbstractSchemaProvider`. | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                               |
| `ISchemaProvider` (current)                 | Read the live database schema. One per application.                            | `UseSchemaSource<T>()` (or via a provider package, e.g. `UsePostgres(...)`)                |
| `ISchemaPolicy`                             | Validate the merged desired schema.                                            | `AddSchemaPolicy<T>()`                                                                     |
| `IMigrationPlanTransformer`                 | Rewrite or reorder the generated plan.                                         | `AddPlanTransformer<T>()`                                                                  |
| `IMigrationPolicy`                          | Validate the final plan before execution.                                      | `AddMigrationPolicy<T>()`                                                                  |
| `IScriptProvider`                           | Supply raw SQL to run pre- or post-deployment.                                 | `AddScriptProvider<T>()`, `AddScriptFromFile(...)`, `AddScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor`                              | Override how SQL is sent to the database (e.g. logging, custom transactions).  | `UseSqlExecutor<T>()`                                                                      |
| `IMigrationExecutor`                        | Replace the executor entirely (e.g. emit SQL to a file instead of running it). | `UseMigrationExecutor<T>()`                                                                |
| `ISqlPlanner`                               | Add support for another database.                                              | `UseSqlPlanner<T>()`  (or via a provider package, e.g. `UsePostgres(...)`)                 |

## Renaming

Renames are explicit. Call `RenamedFrom(...)` on a schema, table, or column so the comparer can match it to the existing one instead of dropping and recreating:

```csharp
var accounts = Schema("app").Table("accounts").RenamedFrom("users");
accounts.Column("display_name", SqlType.Text).RenamedFrom("name");
```

## Building and testing

```bash
dotnet build
dotnet test
```

## License

See [LICENSE](LICENSE).
