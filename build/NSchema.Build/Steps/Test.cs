using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Run Tests")]
public class Test(IOptions<BuildOptions> options, ICommandRunner commands) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        await commands.Run(
            command: "dotnet",
            arguments: ["test", "--no-build", "--configuration", options.Value.Configuration],
            cancellationToken
        );
    }
}
