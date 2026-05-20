using Microsoft.Extensions.Logging;
using NSchema.Diffing;
using NSchema.Domain.Schema;
using NSchema.Extractors;

namespace NSchema.Migration;

public sealed class DefaultSchemaMigrator(
    ILogger<DefaultSchemaMigrator> logger,
    ISchemaExtractor extractor,
    ISchemaDiffer differ,
    IInstructionExecutor executor,
    DatabaseModel desired
) : ISchemaMigrator
{
    public async Task<MigrationPlan> Plan(CancellationToken cancellationToken = default)
    {
        var current = await extractor.Extract(cancellationToken);
        var instructions = differ.Diff(current, desired);
        return new MigrationPlan(instructions);
    }

    public async Task Apply(MigrationPlan plan, ExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (plan.IsEmpty)
        {
            logger.LogInformation("Schema is already up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} schema change(s).", plan.Instructions.Count);
        await executor.Execute(plan.Instructions, options, cancellationToken);
    }
}
