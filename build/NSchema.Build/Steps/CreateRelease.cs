using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Create GitHub Release")]
public class CreateRelease(
    ILogger<CreateRelease> logger,
    IOptions<BuildOptions> options,
    IPipelineContext context,
    ICommandRunner commands
) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var projectInfo = context.State.Get<ProjectInfo>() ?? throw new Exception("Project info not found in state.");

        if (projectInfo.Version.IsPrerelease)
        {
            logger.LogInformation("Skipping GitHub Release for prerelease version {Version}; tag has still been pushed.", projectInfo.Version);
            return;
        }

        var changelog = context.State.Get<ChangelogEntry>() ?? throw new Exception("Changelog entry not found in state.");

        var tag = $"v{projectInfo.Version}";
        var notesFile = context.FileSystem.CurrentDirectory
            .GetDirectory(options.Value.TempDirectory)
            .GetFile($"release-notes-{projectInfo.Version}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(notesFile.AbsolutePath)!);
        await File.WriteAllTextAsync(notesFile.AbsolutePath, changelog.Body, cancellationToken);

        logger.LogInformation("Creating GitHub Release {Tag}.", tag);
        await commands.Run(
            "gh",
            ["release", "create", tag, "--title", tag, "--notes-file", notesFile.AbsolutePath],
            cancellationToken
        );
    }
}
