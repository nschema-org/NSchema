using Microsoft.Extensions.Logging;
using NSchema.Domain.Schema;
using NSchema.Migration.Comparison;
using NSchema.Migration.Execution;
using NSchema.Migration.Extraction;

namespace NSchema.Migration;

public sealed class DefaultSchemaMigrator(
    ILogger<DefaultSchemaMigrator> logger,
    ISchemaExtractor extractor,
    ISchemaComparer comparer,
    IInstructionExecutor executor,
    DatabaseModel desired
) : ISchemaMigrator
{
    public async Task<MigrationPlan> Plan(DatabaseModel target, CancellationToken cancellationToken = default)
    {
        string[] schemas = target.Schemas.Select(s => s.Name).ToArray();
        var current = await extractor.Extract(schemas, cancellationToken);
        var instructions = comparer.Compare(current, desired);
        return new MigrationPlan(instructions);
    }

    public async Task Apply(MigrationPlan plan, ExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (plan.IsEmpty)
        {
            logger.LogInformation("Schema is already up to date.");
            return;
        }

        logger.LogDebug("Applying {Count} schema change(s).", plan.Instructions.Count);
        await executor.Execute(plan.Instructions, options, cancellationToken);
    }
}
