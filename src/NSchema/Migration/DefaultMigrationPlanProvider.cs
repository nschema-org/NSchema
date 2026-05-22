using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// Default implementation of the migration plan provider that orchestrates the process of generating a migration plan.
/// </summary>
/// <param name="currentProvider">The provider for retrieving the current schema state.</param>
/// <param name="desiredProviders">The collection of providers for retrieving the desired schema state.</param>
/// <param name="scriptProviders">The collection of providers for retrieving pre- and post-deployment scripts.</param>
/// <param name="schemaAggregator">The service responsible for aggregating multiple desired schema states into a single cohesive schema.</param>
/// <param name="comparer">The service responsible for comparing the current and desired schemas and generating an initial migration plan.</param>
/// <param name="schemaPolicies">The collection of policies to validate the desired schema.</param>
/// <param name="planTransformers">The collection of transformers to modify the generated migration plan.</param>
/// <param name="migrationPolicies">The collection of policies to validate the final migration plan.</param>
internal sealed class DefaultMigrationPlanProvider(
    ICurrentSchemaProvider currentProvider,
    IEnumerable<IDesiredSchemaProvider> desiredProviders,
    IEnumerable<IDeploymentScriptProvider> scriptProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    IEnumerable<ISchemaPolicy> schemaPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> migrationPolicies
) : IMigrationPlanProvider
{
    public async Task<MigrationPlan> ComputeMigrationPlan(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Run all registered schema validation policies.
        var schemaErrors = schemaPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (schemaErrors.Count > 0)
        {
            throw new PolicyViolationException(schemaErrors);
        }

        var schemasInScope = desiredSchema.Schemas.Select(s => s.Name)
            .Concat(desiredSchema.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Get current schema state.
        var currentSchema = await currentProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var migrationPlan = comparer.Compare(currentSchema, desiredSchema);

        // Collect deployment scripts and inject into the plan.
        var providerList = scriptProviders.ToList();
        if (providerList.Count > 0)
        {
            var preLists = await Task.WhenAll(providerList.Select(p => p.GetPreDeploymentScripts(cancellationToken)));
            var postLists = await Task.WhenAll(providerList.Select(p => p.GetPostDeploymentScripts(cancellationToken)));
            var preActions = preLists.SelectMany(s => s).Select(MigrationAction (s) => new RunPreDeploymentScript(s));
            var postActions = postLists.SelectMany(s => s).Select(MigrationAction (s) => new RunPostDeploymentScript(s));
            migrationPlan = new MigrationPlan([.. preActions, .. migrationPlan.Actions, .. postActions]);
        }

        // Apply all registered plan transformers in order.
        migrationPlan = planTransformers.Aggregate(migrationPlan, (p, t) => t.Transform(p));

        // Run all registered action policies against the transformed plan.
        var migrationErrors = migrationPolicies.SelectMany(p => p.Validate(migrationPlan)).ToList();
        if (migrationErrors.Count > 0)
        {
            throw new PolicyViolationException(migrationErrors);
        }

        return migrationPlan;
    }
}
