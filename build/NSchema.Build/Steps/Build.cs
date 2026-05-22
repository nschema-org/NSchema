using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Build Solution")]
public class Build(IOptions<BuildOptions> options, ICommandRunner commands) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        await commands.Run(
            command: "dotnet",
            arguments: ["build", "--no-restore", "--configuration", options.Value.Configuration],
            cancellationToken
        );
    }
}
