using NSchema.Migration.Actions;
using NSchema.Policies;

namespace NSchema.Migration;

public sealed class DefaultSchemaMigrator(
    ICurrentSchemaProvider currentProvider,
    IEnumerable<IDesiredSchemaProvider> desiredProviders,
    IEnumerable<IDeploymentScriptProvider> scriptProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    IEnumerable<ISchemaPolicy> schemaValidationPolicies,
    IEnumerable<IMigrationPlanTransformer> planTransformers,
    IEnumerable<IMigrationPolicy> actionValidationPolicies
) : ISchemaMigrator
{
    public async Task<MigrationPlan> Plan(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Run all registered schema validation policies.
        var schemaErrors = schemaValidationPolicies.SelectMany(p => p.Validate(desiredSchema)).ToList();
        if (schemaErrors.Count > 0)
        {
            throw new PolicyViolationException(schemaErrors);
        }

        string[] schemasInScope = desiredSchema.Schemas.Select(s => s.Name)
            .Concat(desiredSchema.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Get current schema state.
        var currentSchema = await currentProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var schemaPlan = comparer.Compare(currentSchema, desiredSchema);

        // Collect deployment scripts and inject into the plan.
        var providerList = scriptProviders.ToList();
        if (providerList.Count > 0)
        {
            var preActions = providerList
                .SelectMany(p => p.PreDeploymentScripts)
                .Select(MigrationAction (s) => new RunPreDeploymentScript(s));
            var postActions = providerList
                .SelectMany(p => p.PostDeploymentScripts)
                .Select(MigrationAction (s) => new RunPostDeploymentScript(s));
            schemaPlan = new MigrationPlan([.. preActions, .. schemaPlan.Actions, .. postActions]);
        }

        // Apply all registered plan transformers in order.
        schemaPlan = planTransformers.Aggregate(schemaPlan, (p, t) => t.Transform(p));

        // Run all registered action policies against the transformed plan.
        var actionErrors = actionValidationPolicies.SelectMany(p => p.Validate(schemaPlan)).ToList();
        if (actionErrors.Count > 0)
        {
            throw new PolicyViolationException(actionErrors);
        }

        return schemaPlan;
    }
}
