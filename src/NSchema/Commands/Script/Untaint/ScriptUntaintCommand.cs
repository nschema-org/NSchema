using System.CommandLine;
using NSchema.Configuration;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.State;
using NSchema.State.Locks;

namespace NSchema.Commands.Script.Untaint;

internal static class ScriptUntaintCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The declared name of the script to record as executed.",
    };

    public static Command Create()
    {
        var command = new Command("untaint", "Record a script as executed without running it, so later plans skip it — e.g. after rebuilding lost state with refresh.");

        command.Arguments.Add(NameArgument);
        command.Options.AddRange(ScriptUntaintOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ScriptUntaintConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<ScriptUntaintConfiguration>(result, environment, cancellationToken);
        new ScriptUntaintConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        var name = parseResult.GetValue(NameArgument)!;

        // The desired schema is configured because the hash to record comes from the script's declaration.
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDesiredSchema()
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        var locked = await app.Locks.Acquire(new AcquireLockArguments("script untaint") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
        }

        try
        {
            return await UntaintUnderLock(app, name, cancellationToken);
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }

    private static async Task<int> UntaintUnderLock(CliApplication app, string name, CancellationToken cancellationToken)
    {
        var read = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            app.Messenger.ReportDiagnostics(read.Diagnostics);
            return ExitCodes.Error;
        }

        if (read.Value?.State is not { } state)
        {
            app.Messenger.Warn($"No state has been recorded yet. Run refresh to capture the schema first, then untaint.");
            return ExitCodes.Error;
        }

        if (state.FindScript(name) is not null)
        {
            app.Messenger.Warn($"Script '{name}' is already recorded as executed. To accept a changed body, taint it first, then untaint.");
            return ExitCodes.Error;
        }

        // The recorded identity is the declaration's name and body hash — the same values an apply would record,
        // read from the expanded desired project.
        var project = (await app.ProjectDefinition.GetProject(PlanningScope.All, cancellationToken)).Require();
        var declaration = project.FindScript(name);
        if (declaration is null || declaration.RunCondition != RunCondition.Once)
        {
            app.Messenger.Warn($"Script '{name}' is not declared as a RUN ONCE script in this project; there is nothing to record.");
            return ExitCodes.Error;
        }

        var written = await app.State.Write(new StateWriteArguments(state.RecordExecution([declaration], DateTimeOffset.UtcNow)), cancellationToken);
        if (written.IsFailure)
        {
            app.Messenger.ReportDiagnostics(written.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Recorded '{name}' as executed without running it. Later plans will skip it.");
        return ExitCodes.NoChanges;
    }
}
