using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<MigrationOptions>(o => o.DestructiveActionPolicy = policy);
        return this;
    }

    /// <summary>
    /// Configures the operation the migration run performs (<see cref="MigrationOperation.Plan"/> or
    /// <see cref="MigrationOperation.Apply"/>).
    /// </summary>
    /// <param name="operation">The operation to perform.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder RunOperation(MigrationOperation operation)
    {
        Services.Configure<MigrationOptions>(o => o.Operation = operation);
        return this;
    }

    /// <summary>
    /// Configures the application to perform a dry run, where the migration plan will be generated and logged but not executed against the database.
    /// </summary>
    /// <param name="dryRun">Whether to enable dry run mode. Defaults to true.</param>
    /// <returns>The application builder, for chaining.</returns>
    [Obsolete("Use WithOperation(MigrationOperation.Plan) instead. DryRunOnly will be removed in a future major version.")]
    public NSchemaApplicationBuilder DryRunOnly(bool dryRun = true)
    {
        return RunOperation(dryRun ? MigrationOperation.Plan : MigrationOperation.Apply);
    }

    /// <summary>
    /// Scopes the migration to a specific set of schema names.
    /// </summary>
    /// <param name="schemaNames">The schema names to include in the migration.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder ForSchemas(params string[] schemaNames)
    {
        Services.Configure<MigrationOptions>(o => o.SchemaNames = schemaNames);
        return this;
    }
}
