using System.CommandLine;
using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Services.Reporting;

namespace NSchema.Commands;

/// <summary>
/// Runs the preamble every configured command shares.
/// </summary>
internal static class CommandRunner
{
    /// <param name="parseResult">The parsed command line.</param>
    /// <param name="configure">Applies the resolved configuration to the builder. Runs only once the configuration validates.</param>
    /// <param name="command">The command's own work, given the built application.</param>
    /// <param name="validator">The configuration validator, or <see langword="null"/> for a command with nothing to validate.</param>
    /// <param name="announceEnvironment">
    ///     Whether to print the environment banner. False for a command whose stdout is a payload rather than a report
    ///     (<c>state pull</c> to stdout), so redirection stays byte-clean.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <typeparam name="TConfiguration">The command's configuration model.</typeparam>
    public static async Task<int> Run<TConfiguration>(ParseResult parseResult,
        Func<CliApplicationBuilder, TConfiguration, CliApplicationBuilder> configure,
        Func<CommandContext<TConfiguration>, CancellationToken, Task<int>> command,
        IValidator<TConfiguration>? validator,
        bool announceEnvironment,
        CancellationToken cancellationToken
    ) where TConfiguration : class, IBindable, new()
    {
        // App-free by design: a configuration failure has to be reportable before there is an application to report
        // through, and this is the same messenger the built application would carry.
        var messenger = ReporterFactory.CreateMessenger(parseResult);
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);

        var resolved = validator is null
            ? await ConfigurationFactory.Load<TConfiguration>(parseResult, environment, cancellationToken)
            : await ConfigurationFactory.Load(parseResult, environment, validator, cancellationToken);

        if (resolved.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        var configuration = resolved.Require();

        var built = configure(CliApplicationBuilder.Create(parseResult), configuration).Build();
        if (built.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();
        if (announceEnvironment)
        {
            app.Messenger.ReportEnvironment(environment);
        }

        return await command(new CommandContext<TConfiguration>(app, configuration, parseResult, environment), cancellationToken);
    }
}
