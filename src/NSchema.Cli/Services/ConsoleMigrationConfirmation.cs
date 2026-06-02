using NSchema.Cli.Configuration;
using NSchema.Hosting;
using NSchema.Migration.Plan;

namespace NSchema.Cli.Services;

/// <summary>
/// An <see cref="IMigrationConfirmation"/> that prompts on the terminal before applying changes,
/// </summary>
internal sealed class ConsoleMigrationConfirmation(NSchemaConfiguration configuration) : IMigrationConfirmation
{
    public ValueTask<bool> Confirm(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine($"NSchema will execute {plan.Actions.Count} action(s) against the database.");

        if (configuration.AutoApprove)
        {
            Console.WriteLine("Auto-approve is enabled; skipping confirmation.");
            return ValueTask.FromResult(true);
        }

        Console.Write("Do you want to apply these changes? Only 'yes' will be accepted: ");
        var response = Console.ReadLine();
        var approved = string.Equals(response?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult(approved);
    }
}
