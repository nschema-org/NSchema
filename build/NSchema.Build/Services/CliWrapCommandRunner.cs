using CliWrap;
using CliWrap.EventStream;
using Hamelin;
using Microsoft.Extensions.Logging;

namespace NSchema.Build.Services;

public class CliWrapCommandRunner(ILogger<CliWrapCommandRunner> logger, IPipelineContext context) : ICommandRunner
{
    public async Task Run(string command, string[] arguments, CancellationToken cancellationToken)
    {
        var cmd = Cli.Wrap(command)
            .WithArguments(arguments)
            .WithWorkingDirectory(context.CurrentDirectory)
            .WithValidation(CommandResultValidation.None);

        logger.LogInformation("Running command: {Command}", cmd);

        await foreach (var cmdEvent in cmd.ListenAsync(cancellationToken))
        {
            switch (cmdEvent)
            {
                case StartedCommandEvent started:
                    logger.LogInformation("Process started; ID: {ProcessId}", started.ProcessId);
                    break;
                case StandardOutputCommandEvent stdOut:
                    logger.LogInformation("{Output}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    logger.LogError("{Error}", stdErr.Text);
                    break;
                case ExitedCommandEvent exited:
                    logger.LogInformation("Process exited; Code: {ExitCode}", exited.ExitCode);
                    if (exited.ExitCode != 0)
                    {
                        throw new Exception($"Command {command} returned exit code {exited.ExitCode}");
                    }
                    break;
            }
        }
    }
}
