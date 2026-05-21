using NSchema.Comparison;
using NSchema.Current;
using NSchema.Desired;
using NSchema.Migration;
using NSchema.Validation;

namespace NSchema.Hosting;

public sealed class DefaultNSchemaRunner(
    ICurrentSchemaProvider currentProvider,
    IEnumerable<IDesiredSchemaProvider> desiredProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    ISchemaMigrator migrator,
    IEnumerable<ISchemaValidationPolicy> schemaValidationPolicies,
    IEnumerable<IMigrationActionPolicy> migrationActionPolicies
) : INSchemaRunner
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Run all registered schema validation policies.
        var schemaErrors = schemaValidationPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (schemaErrors.Count > 0)
            throw new SchemaValidationException(schemaErrors);

        string[] schemasInScope = desiredSchema.Schemas.Select(s => s.Name).ToArray();

        // Get current schema state.
        var current = await currentProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var plan = comparer.Compare(current, desiredSchema);

        // Run all registered migration action policies.
        var actionErrors = migrationActionPolicies.SelectMany(p => p.Validate(plan)).ToList();
        if (actionErrors.Count > 0)
            throw new MigrationActionException(actionErrors);

        // Migrate to the desired schema.
        await migrator.Migrate(plan, cancellationToken);
    }
}
