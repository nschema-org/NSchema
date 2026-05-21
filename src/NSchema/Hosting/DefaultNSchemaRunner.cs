using Microsoft.Extensions.Options;
using NSchema.Comparison;
using NSchema.Current;
using NSchema.Desired;
using NSchema.Migration;
using NSchema.Validation;

namespace NSchema.Hosting;

public sealed class DefaultNSchemaRunner(
    IOptions<MigrationOptions> options,
    ICurrentSchemaProvider currentProvider,
    IEnumerable<IDesiredSchemaProvider> desiredProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    ISchemaMigrator migrator,
    IEnumerable<ISchemaValidationPolicy> validationPolicies
) : INSchemaRunner
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Run all registered validation policies against the desired schema.
        var errors = validationPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (errors.Count > 0)
        {
            throw new SchemaValidationException(errors);
        }

        string[] schemasInScope = desiredSchema.Schemas.Select(s => s.Name).ToArray();

        // Get current schema state.
        var current = await currentProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var plan = comparer.Compare(current, desiredSchema);

        // Migrate to the desired schema.
        await migrator.Migrate(plan, options.Value, cancellationToken);
    }
}
