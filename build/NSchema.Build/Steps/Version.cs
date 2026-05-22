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

        if (versions.Count == 0)
        {
            logger.LogInformation("No existing versions found for package {PackageName}.", projectInfo.Name);
            if (projectInfo.Version != NuGetVersion.Parse("0.0.1"))
            {
                throw new Exception(
                    $"No existing versions found for package {projectInfo.Name}. The first version must be 0.0.1.");
            }
        }

        var match = versions.FirstOrDefault(c => c == projectInfo.Version);
        if (match != null)
        {
            throw new Exception("Package version already exists.");
        }

        var latestVersion = versions.Max(v => v);
        if (latestVersion != null)
        {
            Queue<int> versionParts = new([projectInfo.Version.Major, projectInfo.Version.Minor, projectInfo.Version.Patch, projectInfo.Version.Revision]);
            Queue<int> latestVersionParts = new([latestVersion.Major, latestVersion.Minor, latestVersion.Patch, latestVersion.Revision]);

            while (versionParts.Count != 0)
            {
                var part = versionParts.Dequeue();
                var latestPart = latestVersionParts.Dequeue();

                if (part < latestPart)
                {
                    throw new Exception($"Project version {projectInfo.Version} is not greater than the latest version {latestVersion}.");
                }

                if (part > latestPart + 1)
                {
                    throw new Exception($"Project version {projectInfo.Version} is not a valid increment of the latest version {latestVersion}.");
                }

                if (part == latestPart + 1)
                {
                    // If we are incrementing the version, we can stop checking further parts
                    break;
                }
            }

            while (versionParts.Count != 0)
            {
                var part = versionParts.Dequeue();
                if (part != 0)
                {
                    throw new Exception("When incrementing a version number, all remaining version parts must be 0.");
                }
            }
        }

        logger.LogInformation("Version {Version} is valid and can be used for package {PackageName}.", projectInfo.Version, projectInfo.Name);
    }
}
