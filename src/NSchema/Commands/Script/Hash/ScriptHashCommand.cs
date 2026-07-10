using System.CommandLine;
using NSchema.Configuration;

namespace NSchema.Commands.Script.Hash;

internal static class ScriptHashCommand
{
    private static readonly Argument<string?> NameArgument = new("name")
    {
        Description = "The declared name of a run-once script. Omit to list every run-once declaration with its hash.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("hash", "Compute the body hash of the project's run-once scripts — the identity the state ledger records, e.g. for hand-editing pulled state.");

        command.Arguments.Add(NameArgument);

        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        await ConfigurationFactory.Load<ScriptHashConfiguration>(parseResult, environment, cancellationToken);
        var name = parseResult.GetValue(NameArgument);

        // Only the project's DDL is read — no backend, no provider, no database, no lock.
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDesiredSchema(environment)
            .Build();

        var project = (await app.DesiredSchema.GetProject(cancellationToken: cancellationToken)).Project;

        if (name is null)
        {
            app.Messenger.ReportEnvironment(environment);
            app.Messenger.ReportScriptHashes(project.All());
            return ExitCodes.NoChanges;
        }

        if (project.FindScript(name) is not { } declaration)
        {
            app.Messenger.Warn($"Script '{name}' is not declared as a RUN ONCE script in this project.");
            return ExitCodes.Error;
        }

        // The hash itself is the query's result: bare on stdout, so `$(nschema script hash x)` just works.
        await Console.Out.WriteLineAsync(declaration.Hash);
        return ExitCodes.NoChanges;
    }
}
