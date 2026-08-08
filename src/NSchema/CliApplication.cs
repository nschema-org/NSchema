using NSchema.Deployment;
using NSchema.Operations;
using NSchema.Plan.PlanFile;
using NSchema.Project;
using NSchema.State;
using NSchema.State.Locks;

namespace NSchema;

/// <summary>
/// The CLI's composition of a built <see cref="NSchemaApplication"/>.
/// </summary>
internal sealed class CliApplication(NSchemaApplication app, IConsoleReporter reporter) : IDisposable
{
    /// <summary>
    /// The console reporter for the application.
    /// </summary>
    public IConsoleReporter Reporter { get; } = reporter;

    /// <inheritdoc cref="NSchemaApplication.Operations"/>
    public INSchemaOperations Operations => app.Operations;

    /// <inheritdoc cref="NSchemaApplication.Locks"/>
    public IStateLockManager Locks => app.Locks;

    /// <inheritdoc cref="NSchemaApplication.Database"/>
    public IDatabaseProvider Database => app.Database;

    /// <inheritdoc cref="NSchemaApplication.Project"/>
    public IProjectProvider Project => app.Project;

    /// <inheritdoc cref="NSchemaApplication.PlanFile"/>
    public IPlanFileManager PlanFile => app.PlanFile;

    /// <inheritdoc cref="NSchemaApplication.State"/>
    public IDatabaseStateManager State => app.State;

    /// <inheritdoc cref="NSchemaApplication.Services"/>
    public IServiceProvider Services => app.Services;

    public void Dispose() => app.Dispose();
}
