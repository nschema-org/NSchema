# Concepts

This page goes over the core concepts in NSchema in more detail.

## Domain model

NSchema works around a simple domain model of schemas, tables, columns, indexes, and constraints. The model is designed to be flexible enough to represent the features of any relational database, while still being simple and intuitive to work with.

These models are used to represent both the desired state (what you want) and the current state (what the database has), so they can be compared symmetrically and transformed as needed.

Because they're just .NET objects, the schema can be constructed in any way you like. The built-in `AbstractSchemaProvider` provides a convenient entry point, but you could just as easily read from a JSON file, generate it from code, or even construct it from the database itself.

## Pipeline

This section goes over the high-level pipeline steps that every NSchema application flows through. Most stages have an interface you can swap or extend; these are covered in more detail below and in [Extension points](extension-points.md).

### Planning

This section of the pipeline is where the migration plan is generated. It runs on every execution, even for an `Apply`, so that stale plans aren't accidentally applied.

1. **Resolve desired schemas.** Load the target schema(s) from one or more registered sources.
2. **Combine desired schemas.** Combine the desired schemas into a single database schema.
3. **Validate the desired schema.** Run any registered schema policies to validate things like naming, required columns, banned types, etc.
4. **Read current state.** Load the current schema from your target database, or the state store.
5. **Compare schemas.** The current and desired schemas are compared to produce a `MigrationPlan`.
6. **Transform the plan.** Any custom transformations are applied to the plan. This is where actions are reordered to respect dependencies, or where custom actions are injected.
7. **Validate the plan.** Validate the plan using any registered policies. If configured, the built-in `DestructiveActionMigrationPolicy` will error on any destructive actions.
8. **Compile the plan.** The migration plan is compiled into an executable unit of work.

### Applying

This section runs only for an `Apply` operation. It takes the compiled plan and executes it against the database.

1. **Execute the migration.** Takes the compiled migration from the Planning phase, and executes it against the target.
2. **State capture.** After a successful apply, the resulting schema is captured to the state store (if configured) so that future plans can be generated against it.

### Refresh

The `Refresh` operation captures the current live schema to the state store without doing any planning or applying. This is useful for recording drift that happened between applies, or for initializing the state store with the current schema.

## Schema providers

All schemas, both desired and current, are resolved through an `ISchemaProvider` interface. This allows providers to be swapped in and out for different purposes, and for the same provider to be used as either a desired or current source depending on how it's registered.

While only one current provider can be registered, multiple desired providers are supported and will be combined into a single schema using an implementation of `ISchemaAggregator`, which can be overridden.

The default schema aggregator will merge declared schemas of the same name, but throw exceptions on duplicate tables. This allows you to organize your schemas in different ways, e.g. by feature or bounded context, and have them merged together at runtime.

### Schema scope

The `ISchemaProvider.GetSchema(...)` method takes an optional list of schema names to read. When `null` or empty, the provider is expected to return its full schema. This allows a provider that supports it (e.g. Postgres) to be used as either a desired or current source without changes, and also allows for scoping to a subset of schemas when needed.

This allows you to deploy schemas independently of one another, even when they're contained within the same assembly, or to build tools that target a single schema without needing to read or understand the full database.

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

- `ISqlPlanner` generates the SQL statement for each action in the plan. This is typically implemented in database providers like `NSchema.Postgres`.
- `ISqlExecutor` takes the generated SQL and executes it against the database. There is a default implementation that has simple transaction management, but you could replace it with one that adds logging, retries, or other features.

The default compiler compiles the plan into a SQL script, but you could replace it with a compiler that emits a C# class, a PowerShell script, or anything else you can execute.
