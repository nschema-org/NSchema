using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Pack NuGet Package")]
public class Pack(IOptions<BuildOptions> options, ICommandRunner commands) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        await commands.Run(
            command: "dotnet",
            arguments: [
                "pack",
                "--no-build",
                "--configuration", options.Value.Configuration,
                "--output", options.Value.ArtifactsDirectory
            ],
            cancellationToken
        );
    }
}
