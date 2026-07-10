using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Services.Reporting;
using NSchema.State;
using NSchema.State.Storage;

namespace NSchema;

/// <summary>
/// The CLI's composition of a built <see cref="NSchemaApplication"/> with the console surfaces it renders through.
/// A command reaches the engine (operations, locks, schema/plan reads) and the console (messenger, presenter) through
/// this one handle, so neither console surface has to live in the DI container.
/// </summary>
internal sealed class CliApplication(NSchemaApplication app, IConsoleMessenger messenger, IConsolePresenter presenter) : IDisposable
{
    /// <summary>The line-level messenger: status, outcomes, diagnostics, and the like.</summary>
    public IConsoleMessenger Messenger { get; } = messenger;

    /// <summary>The presenter for an operation's structured output: the diff, schema, SQL plan, and deployment scripts.</summary>
    public IConsolePresenter Presenter { get; } = presenter;

    /// <inheritdoc cref="NSchemaApplication.Operations"/>
    public INSchemaOperations Operations => app.Operations;

    /// <inheritdoc cref="NSchemaApplication.Locks"/>
    public IStateLockCoordinator Locks => app.Locks;

    /// <inheritdoc cref="NSchemaApplication.CurrentSchema"/>
    public ICurrentSchemaProvider CurrentSchema => app.CurrentSchema;

    /// <inheritdoc cref="NSchemaApplication.PlanFile"/>
    public IPlanFileWriter PlanFile => app.PlanFile;

    /// <inheritdoc cref="NSchemaApplication.State"/>
    public ISchemaStateManager State => app.State;

    /// <inheritdoc cref="NSchemaApplication.Services"/>
    public IServiceProvider Services => app.Services;

    public void Dispose() => app.Dispose();
}
