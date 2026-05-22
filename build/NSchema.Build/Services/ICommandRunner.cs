namespace NSchema.Build.Services;

public interface ICommandRunner
{
    Task Run(string command, string[] arguments, CancellationToken cancellationToken);
}
