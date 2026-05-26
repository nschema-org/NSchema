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
    /// Configures the application to perform a dry run, where the migration plan will be generated and logged but not executed against the database.
    /// </summary>
    /// <param name="dryRun">Whether to enable dry run mode. Defaults to true.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder DryRunOnly(bool dryRun = true)
    {
        Services.Configure<MigrationOptions>(o => o.DryRun = dryRun);
        return this;
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
