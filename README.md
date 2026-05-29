# ![NSchema](https://raw.githubusercontent.com/tom-wolfe/NSchema/main/assets/nschema-logo-horizontal.png)

NSchema is a declarative database schema migration library for .NET.

You describe the schema you want in C#. NSchema compares it against the current state of your database, then runs the SQL to bring it in line.

Designed to be familiar to .NET devs, with extensibility and safety features enough support many different workflows.

## Quickstart

Check out the [samples](samples/) for more complete sample applications and reference implementations.

## Getting started

Install the core package and a database provider:

```bash
dotnet add package NSchema
dotnet add package NSchema.Postgres   # or another provider
```

Declare a schema by subclassing `AbstractSchemaProvider`:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
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
await app.Apply();
```

On startup, NSchema introspects the database, compares it with your desired schema, and applies the resulting plan.

### Hosting

NSchema should be familiar to any developer who's used ASP .NET. It runs as a hosted application, with `NSchemaApplication.CreateBuilder(...)` producing an `IHostBuilder` that you can configure with your target schema, database provider, configuration, logging, metrics or any other .NET packages that we've come to rely on.

Call `Build()` to get an `NSchemaApplication`, from which you can then `Plan()` or `Apply()`. (Note: you can also run the standard `RunAsync()` extension method, which will use the configured operation for that run, defaulting to `Plan` if none has been specified.)

### Configuration

[TODO: expand on configuration options, e.g. logging, metrics, providers, etc.]

#### Operations: plan and apply

Each run performs one operation, selected by `MigrationOptions.Operation`:

- **`Apply`** (default) — compute the plan and apply it to the database.
- **`Plan`** — compute and render the plan, including the SQL that would run, without touching the database.

Configure it on the builder, or trigger it explicitly:

```csharp
// Configured
builder.RunOperation(MigrationOperation.Plan);
await builder.Build().RunAsync();

// Explicit — overrides the configured operation for this run
var app = builder.Build();
await app.Plan();   // or app.Apply()
```

Both paths run the full host lifecycle. `RunAsync()` uses the configured operation; `Plan()` / `Apply()` override it for that run.

#### Scoping to specific schemas

Set `MigrationOptions.SchemaNames` (via `ForSchemas(...)`) to scope a run to a subset of schemas. Useful for deploying schemas independently of one another, or for tooling that targets a single schema:

```csharp
builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .ForSchemas("app");   // only "app" is read, validated, and diffed
```

Declarations or drops for schemas outside the scope are ignored, so unmanaged schemas in the database are never touched.

Next sections cover the concepts and extension points in more detail.

### Schema declaration

The easiest way to declare a schema is to subclass `AbstractSchemaProvider` as in the quickstart above.

#### Schema declaration

Schemas are declared as follows:

```csharp
using NSchema.Schema;
using NSchema.Schema.Fluent;

public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            // Configure schema here.
            s.Comment("This is the app schema.");
            s.RenamedFrom("old_app");
            s.AsPartial();
        });

        Schema("old_schema", s => s.Dropped());
    }
}
```

- `Table(...)` declares a table within the schema.
- `Grant(...)` grants a role usage on the schema.
- `Comment(...)` adds a comment to the schema.
- `RenamedFrom(...)` marks the schema as renamed from an existing one, so the comparer can match it instead of dropping and recreating.
- `AsPartial()` marks the schema as partial, meaning that tables not declared here won't be dropped. This is useful for shared schemas, or when you want to manage some tables manually.
- `Dropped()` marks the schema as dropped, meaning it will be dropped if it exists.

#### Table declaration

Tables are declared within a schema:

```csharp
public class AppSchema : AbstractSchemaProvider
{
    public AppSchema()
    {
        Schema("app", s =>
        {
            s.Table("users", t => {
                // Configure table here.
                t.Column("id", SqlType.Text, c => c.PrimaryKey("users_pkey"));
                t.Column("email", SqlType.Text, c => c.NotNull());
                t.Column("name", SqlType.Text, c => c.NotNull());
                t.Index("uc_users_email", ["email"], i => i.Unique());

                t.Comment("This is the users table.");
                t.RenamedFrom("old_users");
            });
        });
    }
}
```

- `Column(...)` declares a column with the table.
- `PrimaryKey(...)` declares a primary key constraint on the table.
- `ForeignKey(...)` declares a foreign key constraint on the table.
- `Index(...)` declares an index on the table.
- `Grant(...)` grants a role SELECT, INSERT, UPDATE or DELETE on the table.
- `Comment(...)` adds a comment to the table.
- `RenamedFrom(...)` marks the table as renamed from an existing one, so the comparer can match it instead of dropping and recreating.
- `Dropped()` marks the table as dropped, meaning it will be dropped if it exists. Only necessary when dropping a table from a partial schema, otherwise the comparer will detect it as missing and drop it automatically.

## Concepts

### Domain Model

NSchema works around a simple domain model of schemas, tables, columns, indexes, and constraints. The model is designed to be flexible enough to represent the features of any relational database, while still being simple and intuitive to work with.

These models are used to represent both the desired state (what you want) and the current state (what the database has), so they can be compared symmetrically and transformed as needed.

Because they're just .NET objects, the schema can be constructed in any way you like. The built-in `AbstractSchemaProvider` provides a convenient entry point, but you could just as easily read from a JSON file, generate it from code, or even construct it from the database itself.
#### Fluent API

[TODO: Write docs about the `AbstractSchemaProvider` class.]

### Pipeline

This section goes over the high level pipeline steps that very NSchema application flows through. Most stages have an interface you can swap or extend, and these will be covered in more detail after the pipeline overview.

#### Planning

This section of the pipeline is where the migration plan is generated. It runs on every execution, even for an `Apply`, so that stale plans aren't accidentally applied.

1. **Resolve desired schemas.** Load the target schema(s) from one or more registered sources.
2. **Combine desired schemas.** Combine the desired schemas into a single database schema.
3. **Validate the desired schema.** Run any registered schema policies to validate things like naming, required columns, banned types, etc.
4. **Read current state.** Load the current schema from your target database, or another source.
5. **Compare schemas.** The current and desired schemas are compared to produce a `MigrationPlan`.
6. **Transform the plan.** Any custom transformations are applied to the plan. This is where actions are reordered to respect dependencies, or where custom actions are injected.
7. **Validate the plan.** Validate the plan using any registered policies. If configured, the built-in `DestructiveActionMigrationPolicy` will error on any destructive actions.
8. **Compile the plan.** The migration plan is compiled into an executable unit of work.

#### Applying

This section runs only for an `Apply` operation. It takes the compiled plan and executes it against the database.

1. **Execute the migration.** Takes the compiled migration from the Planning phase, and executes it against the target.

## Schema providers

All schemas, both desired and current, are resolved through an `ISchemaProvider` interface. This allows providers to be swapped in and out for different purposes, and for the same provider to be used as either a desired or current source depending on how it's registered.

While only one current provider can be registered, multiple desired providers are supported and will be combined into a single schema using an implementation `ISchemaAggregator`, which can be overridden.

The default schema aggregator will merge declared schemas of the same name, but throw exceptions on duplicate tables. This allows you to organize your schemas in different ways, e.g. by feature or bounded context, and have them merged together at runtime.

### Schema scope

The `ISchemaProvider.GetSchema(...)` method takes an optional list of schema names to read. When `null` or empty, the provider is expected to return its full schema. This allows a provider that supports it (e.g. Postgres) to be used as either a desired or current source without changes, and also allows for scoping to a subset of schemas when needed.

This allow you to deploy schemas independently of one another, even when they're contained within the same assembly, or to build tools that target a single schema without needing to read or understand the full database.

By default, the scope of a migration is equal to the full set of schemas returned by the registered desired providers, but it can also be configured explicitly via `MigrationOptions.SchemaNames` or the `ForSchemas(...)` extension method.

## Schema policies

Schema policies are used to validate the desired schema before any comparison or planning is done. This is where you can enforce naming conventions, required columns, banned types, or any other rules you want to apply to your schema.

Schema policies are implemented using `ISchemaPolicy` and are registered with `AddSchemaPolicy<T>()`. If the policy returns any errors, execution will halt, preventing bad schemas from being applied.

## Schema comparison

The schema comparer is responsible for taking the current and desired schemas and producing a migration plan. The migration plan takes the form of a list of actions to perform, such as creating or dropping tables, adding or removing columns, etc.

The default comparer supports all the core features of the domain model, but you can replace it with your own implementation of `ISchemaComparer` if you have special requirements.

## Plan transformation

The plan transformation step allows the migration plan to be modified before it's validated and executed. This is where actions are re-ordered to respect dependencies, or custom actions can be injected that aren't directly related to schema changes (e.g. data migrations, cache invalidation, etc.)

Transformations are implemented by creating a class that implements `IMigrationPlanTransformer`, and registered with `AddPlanTransformer<T>()`.

## Migration policies

Migration policies are used to validate the generated migration plan before it's executed. This is where you can enforce rules about what kinds of changes are allowed, e.g. preventing destructive actions like dropping tables or columns.

Migration policies are implemented using `IMigrationPolicy` and registered with `AddMigrationPolicy<T>()`. If any policy returns errors, execution will halt, preventing bad plans from being applied.

## Migration compilation

The migration compiler is responsible for taking the validated migration plan and compiling it into an executable unit of work, along with a preview of the work that will be done.

For a complete override, you can implement `IMigrationCompiler` and register with `UseMigrationCompiler<T>()`, but if you're targeting a SQL database, the default compiler offers two extension points:

* `ISqlPlanner` generates the SQL statement for each action in the plan. This is typically implemented in database providers like `NSchema.Postgres`.
* `ISqlExecutor` takes the generated SQL and executes it against the database. There is a default implementation that has simple transaction management, but you could replace it with one that adds logging, retries, or other features.

The default compiler compiles the plan into a SQL script, but you could replace it with a compiler that emits a C# class, a PowerShell script, or anything else you can execute.

## Extension points

As a summary to the above, here is a table of the extension points in NSchema.
Everything in the pipeline is registered through DI. You can replace defaults or add to the enumerable extension points.

| Interface                   | Purpose                                                                                                 | Registered via                                                                             |
|-----------------------------|---------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| `ISchemaProvider` (desired) | Contribute schemas to the desired state. Usually via `AbstractSchemaProvider`.                          | `AddSchema<T>()` / `AddSchemasFromAssembly[Containing]<T>()`                               |
| `ISchemaProvider` (current) | Read the current database schema. One per application.                                                  | `UseCurrentSchema<T>()` (or via a provider package, e.g. `UsePostgres(...)`)               |
| `ISchemaPolicy`             | Validate the merged desired schema.                                                                     | `AddSchemaPolicy<T>()`                                                                     |
| `IMigrationPlanTransformer` | Rewrite or reorder the generated plan.                                                                  | `AddPlanTransformer<T>()`                                                                  |
| `IMigrationPolicy`          | Validate the final plan before execution.                                                               | `AddMigrationPolicy<T>()`                                                                  |
| `IScriptProvider`           | Supply raw SQL to run pre- or post-deployment.                                                          | `AddScriptProvider<T>()`, `AddScriptFromFile(...)`, `AddScriptsFromEmbeddedResources(...)` |
| `ISqlExecutor`              | Override how SQL is sent to the database (e.g. logging, custom transactions).                           | `UseSqlExecutor<T>()`                                                                      |
| `IMigrationCompiler`        | Replace how a plan is compiled into an executable unit (e.g. emit SQL to a file instead of running it). | `UseMigrationCompiler<T>()`                                                                |
| `ISqlPlanner`               | Add support for another database.                                                                       | `UseSqlPlanner<T>()`  (or via a provider package, e.g. `UsePostgres(...)`)                 |

### Less useful extension points

These extension points are less commonly used, but still available for advanced scenarios.

| Interface           | Purpose                                                               | Registered via                         |
|---------------------|-----------------------------------------------------------------------|----------------------------------------|
| `ISchemaAggregator` | Combine multiple desired schemas into a single schema for comparison. | `AddSingleton<ISchemaAggregator, T>()` |
| `ISchemaComparer`   | Compare the current and desired schemas to produce a migration plan.  | `AddSingleton<ISchemaComparer, T>()`   |

## License

See [LICENSE](LICENSE).
