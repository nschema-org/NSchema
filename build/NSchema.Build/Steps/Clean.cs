using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;

namespace NSchema.Build.Steps;

[DisplayName("Clean Directories")]
public class Clean(ILogger<Clean> logger, IOptions<BuildOptions> options, IPipelineContext context) : IPipelineStep
{
    public Task Run(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Cleaning temp and artifact directories.");
        var cd = context.FileSystem.CurrentDirectory;
        cd.GetDirectory(options.Value.ArtifactsDirectory).Delete();
        cd.GetDirectory(options.Value.TempDirectory).Delete();
        return Task.CompletedTask;
    }
}
