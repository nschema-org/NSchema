using Microsoft.Extensions.Logging;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationReporter"/> that presents output to the terminal: it renders the plan as a
/// Terraform-style diff via <see cref="IMigrationPlanRenderer"/>, writes user-facing output directly to the
/// console, and fans out to <see cref="ILogger"/> so structured sinks (Datadog, OpenTelemetry, etc.)
/// continue to receive the migration narrative.
/// </summary>
/// <param name="logger">The logger to fan structured output out to.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
internal sealed class DefaultMigrationReporter(ILogger<DefaultMigrationReporter> logger, IMigrationPlanRenderer planRenderer) : IMigrationReporter
{
    public void Info(string message) => Write(LogLevel.Information, message);

    public void Error(string message) => Write(LogLevel.Error, message);

    public void ReportPlan(MigrationPlan plan)
    {
        Write(LogLevel.Information, planRenderer.Render(plan));

        // A blank line separates the diff from following output. This is terminal layout only, so it
        // doesn't go through the logger.
        Console.Out.WriteLine();
    }

    public void ReportPreview(IReadOnlyList<string> statements)
    {
        foreach (var statement in statements)
        {
            Write(LogLevel.Information, statement);
        }
    }

    public void ReportDiagnostics(IReadOnlyList<PolicyError> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var level = diagnostic.Severity switch
            {
                PolicySeverity.Error => LogLevel.Error,
                PolicySeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
            Write(level, $"{diagnostic.PolicyName}: {diagnostic.Message}");
        }
    }

    private void Write(LogLevel level, string message)
    {
        // Forward to ILogger as a single structured Message field for downstream sinks.
        logger.Log(level, "{Message}", message);

        // Errors and warnings go to stderr so they don't get tangled with normal output when piped.
        var writer = level >= LogLevel.Warning ? Console.Error : Console.Out;
        writer.WriteLine(message);
    }
}
