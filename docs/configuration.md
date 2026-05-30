# Configuration

How to host, run, and configure an NSchema application. New to NSchema? Start with the [README](../README.md) quickstart, then come back here.

## Hosting

NSchema should be familiar to any developer who's used ASP.NET. It runs as a hosted application, with `NSchemaApplication.CreateBuilder(...)` producing a builder that you can configure with your target schema, database provider, configuration, logging, metrics, or any other .NET packages that we've come to rely on.

Call `builder.Build()` to get an `NSchemaApplication`, from which you can then run a `Plan()` or `Apply()` operation. You can also use the standard `RunAsync()` extension method, which will use the configured `MigrationOptions.Operation` for that run, defaulting to `Plan` if none has been specified.

## Operations: plan and apply

Each run performs one of the following operations:

- **`Plan`** (default) computes and render the plan, without touching the database.
- **`Apply`** computes the plan and apply it to the database.

The operation can be decided in one of two ways: either by setting `MigrationOptions.Operation`, or by explicitly calling `Plan()` or `Apply()` on the built application:

```csharp
// Configured
builder.RunOperation(MigrationOperation.Plan);
var app = builder.Build();
await app.RunAsync(); // Will run the plan.

// Explicit
await app.Apply(); // Will run the apply, even if the configured operation is Plan.
```

Both paths run the full .NET host lifecycle, so background services, logging, metrics, etc. are all available regardless of how you choose to run.

## Backend state store

By default, NSchema generates plans against the current live state of the database. This is simple and works well for many scenarios, but you can't always guarantee that you'll have access to the database at plan time, and sometimes it's desirable to generate a plan against the last applied state rather than the current live state (e.g. for generating migration scripts in a CI pipeline).

NSchema supports an optional backend state store for tracking the last applied schema. When configured, NSchema will read the last applied schema from the store and generate plans against that instead of the current live state. After a successful apply, NSchema will update the store with the new last applied schema.

To use a backend state store, implement the `ISchemaStateStore` interface and register it with `UseSchemaStateStore<T>()`. NSchema includes a simple `LocalFileSchemaStateStore` that saves the last applied schema to a local file, which can be used for simple scenarios or as a reference implementation for custom stores.

```csharp
builder.UseLocalFileSchemaStateStore("schema_state.json");
```

## Destructive action policy

By default, NSchema will error on any destructive actions (e.g. dropping tables or columns) to prevent accidental data loss. You can change this behavior by configuring the `DestructiveActionPolicy`:

```csharp
builder.WithDestructiveActionPolicy(DestructiveActionPolicy.Warn); // log a warning, but continue with the migration
builder.WithDestructiveActionPolicy(DestructiveActionPolicy.Allow); // allow destructive actions without warning
```

If you need more advanced control, you can implement your own `IMigrationPolicy` and register it with `AddMigrationPolicy<T>()`.

## Scoping to specific schemas

Set `MigrationOptions.SchemaNames` to scope a run to a subset of schemas. Useful for deploying schemas independently of one another:

```csharp
builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .ForSchemas("app");   // only "app" is read, validated, and diffed
```

Declarations or drops for schemas outside the scope are ignored, so unmanaged schemas in the database are never touched.

## Configuring desired schemas

The desired schema(s) are configured by registering one or more `ISchemaProvider` implementations that can supply the target schema. The most common way to do this is to subclass `AbstractSchemaProvider`, but you can also implement `ISchemaProvider` directly if you need more control.

```csharp
builder
    .AddSchema<AppSchema>() // register a single schema provider
    .AddSchemasFromAssemblyContaining<AppSchema>(); // register all providers in the assembly
```

See [Defining schemas](schemas.md) for the full declaration reference.

## Configuring the current schema

The current schema is configured by registering a single `ISchemaProvider` that can read the current state of the database. This is typically done via a provider package like `NSchema.Postgres`, which will include an implementation that reads the schema directly from the database.

```csharp
// Using a provider package:
builder.UsePostgres();

// Or directly:
builder.UseCurrentSchema<PostgresSchemaProvider>();
```

## Adding scripts

You can add pre- or post-deployment scripts to run alongside the generated migration SQL. This is useful for data migrations, cache invalidation, or any other custom logic that needs to run as part of the deployment.

Scripts can also be added from embedded resources, or by implementing `IScriptProvider` directly for more complex scenarios:

```csharp
// Add scripts from files:
builder
    .AddScriptFromFile(ScriptType.PreDeployment, "pre_deploy.sql")
    .AddScriptFromFile(ScriptType.PostDeployment, "post_deploy.sql");

// Add scripts from embedded resources:
builder.AddScriptsFromEmbeddedResources(ScriptType.PreDeployment, typeof(Program).Assembly, "MyNamespace.Scripts.PreDeployment.");

// Add a custom script provider:
builder.AddScriptProvider<CustomScriptProvider>();
```

## Transaction mode

By default, NSchema runs the entire migration inside a single transaction to ensure that either all changes are applied successfully or none at all. However, some databases don't support DDL statements inside transactions, or you may have specific statements that need to run outside of a transaction.

You can configure the transaction mode with `MigrationOptions.TransactionMode`:

```csharp
builder.WithTransactionMode(TransactionMode.Single); // run the entire migration in a single transaction (default)
builder.WithTransactionMode(TransactionMode.None); // run all statements outside of transactions
```

## Schema policies

Schema policies are used to validate the desired schema before any comparison or planning is done:

```csharp
builder.AddSchemaPolicy<TableNamesMustBePluralPolicy>();
```

## Migration policies

Migration policies are used to validate the generated migration plan before it's executed:

```csharp
builder.AddMigrationPolicy<NoDestructiveActionsPolicy>();
```

## Plan transformers

Plan transformers are used to rewrite or reorder the generated migration plan before it's validated and executed:

```csharp
builder.AddPlanTransformer<DependencyOrderingPlanTransformer>();
```
