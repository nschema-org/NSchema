using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Publish NuGet Package")]
public class Publish(IOptions<BuildOptions> options, IPipelineContext context, ICommandRunner commands) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var packageFile = context.FileSystem.CurrentDirectory
            .GetDirectory(options.Value.ArtifactsDirectory)
            .GetFiles("*.nupkg")
            .Single();

        await commands.Run(
            command: "dotnet",
            arguments: [
                "nuget", "push",
                packageFile.AbsolutePath,
                "--source", options.Value.NuGetFeed,
                "--api-key", options.Value.NuGetApiKey
            ],
            cancellationToken
        );
    }
}
