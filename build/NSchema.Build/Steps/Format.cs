using System.ComponentModel;
using Hamelin;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Validate Code Formatting")]
public class Format(ICommandRunner commands) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        await commands.Run(
            command: "dotnet",
            arguments: ["format", "--verify-no-changes"],
            cancellationToken
        );
    }
}
