using System.CommandLine;
using NSchema.Model;

namespace NSchema.Commands.Script.Hash;

internal static class ScriptHashCommand
{
    private static readonly Argument<string?> NameArgument = new("name")
    {
        Description = "The declared name of a script. Omit to list every declaration with its hash.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("hash", "Compute the body hash of the project's scripts.");

        command.Arguments.Add(NameArgument);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run<ScriptHashConfiguration>(
        parseResult,
        configure: (builder, _) => builder.ConfigureDesiredSchema(),
        command: Execute,
        validator: null,
        // Naming a script makes the bare hash the output, so narration is suppressed to keep
        // `$(nschema script hash x)` clean.
        announceEnvironment: parseResult.GetValue(NameArgument) is null,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<ScriptHashConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, _, parseResult, _) = context;
        var name = parseResult.GetValue(NameArgument);

        var project = (await app.Project.GetProject(PlanningScope.All, cancellationToken)).Require();

        if (name is null)
        {
            app.Reporter.ReportScriptHashes(project.ScriptHashes());
            return ExitCodes.NoChanges;
        }

        if (project.FindScript(name) is not { } declaration)
        {
            app.Reporter.Warn($"Script '{name}' is not declared in this project.");
            return ExitCodes.Error;
        }

        // The hash itself is the query's result: bare on stdout, so `$(nschema script hash x)` just works.
        await Console.Out.WriteLineAsync(declaration.Hash.Value);
        return ExitCodes.NoChanges;
    }
}
