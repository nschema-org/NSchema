using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Create Git Tag")]
public class CreateTag(
    ILogger<CreateTag> logger,
    IOptions<BuildOptions> options,
    IPipelineContext context,
    ICommandRunner commands
) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var projectInfo = context.State.Get<ProjectInfo>() ?? throw new Exception("Project info not found in state.");
        var tag = $"v{projectInfo.Version}";

        List<string> tagArgs = ["tag", tag];
        if (!string.IsNullOrEmpty(options.Value.CommitSha))
        {
            tagArgs.Add(options.Value.CommitSha);
        }

        logger.LogInformation("Creating git tag {Tag}.", tag);
        await commands.Run("git", tagArgs.ToArray(), cancellationToken);
        await commands.Run("git", ["push", "origin", tag], cancellationToken);
    }
}
