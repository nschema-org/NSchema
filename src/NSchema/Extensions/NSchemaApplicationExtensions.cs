using Microsoft.Extensions.DependencyInjection;

namespace NSchema.Services;

/// <summary>
/// Exposes the CLI's console surfaces on the application via the same <c>app.X</c> access pattern as
/// <see cref="NSchemaApplication.Operations"/> and <see cref="NSchemaApplication.Locks"/>.
/// </summary>
internal static class NSchemaApplicationExtensions
{
    extension(NSchemaApplication app)
    {
        /// <summary>
        /// The line-level messenger: status, outcomes, diagnostics, and the like.
        /// </summary>
        public IConsoleMessenger Messenger => app.Services.GetRequiredService<IConsoleMessenger>();

        /// <summary>
        /// The rich presenter for an operation's structured output: the diff, schema, SQL plan, and deployment scripts.
        /// </summary>
        public IConsolePresenter Presenter => app.Services.GetRequiredService<IConsolePresenter>();
    }
}
