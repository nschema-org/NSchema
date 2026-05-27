using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Helpers;
using NSchema.Build.Models;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NSchema.Build.Steps;

[DisplayName("Validate Package Version")]
public class Version(
    ILogger<Version> logger,
    IOptions<BuildOptions> options,
    IPipelineContext context
) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var projectInfo = context.State.Get<ProjectInfo>();
        if (projectInfo == null)
        {
            throw new Exception("Project info not found in state.");
        }

        PackageSourceCredential credentials = new(options.Value.NuGetFeed, "dummy", "", true, null);
        var packageSource = new PackageSource(options.Value.NuGetFeed) { Credentials = credentials };
        var repository = Repository.Factory.GetCoreV3(packageSource);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        var versions = (await resource.GetAllVersionsAsync(
            projectInfo.Name,
            new SourceCacheContext(),
            new NuGetLoggerAdapter(logger),
            cancellationToken
        )).ToList();

        if (versions.Any(v => v == projectInfo.Version))
        {
            throw new Exception($"Package version {projectInfo.Version} already exists on the feed.");
        }

        var latestVersion = versions.DefaultIfEmpty().Max();
        if (latestVersion != null && projectInfo.Version <= latestVersion)
        {
            throw new Exception($"Project version {projectInfo.Version} is not greater than the latest version {latestVersion}.");
        }

        if (options.Value.IsPreRelease is { } isPreRelease && isPreRelease != projectInfo.Version.IsPrerelease)
        {
            throw new Exception(
                $"GitHub Release prerelease flag ({isPreRelease}) does not match project version {projectInfo.Version} (IsPrerelease={projectInfo.Version.IsPrerelease}).");
        }

        logger.LogInformation("Version {Version} is valid and can be used for package {PackageName}.", projectInfo.Version, projectInfo.Name);
    }
}
