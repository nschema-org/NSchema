using System.ComponentModel;
using System.Xml;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NuGet.Versioning;

namespace NSchema.Build.Steps;

[DisplayName("Extract Project Information")]
public class ExtractProject(ILogger<ExtractProject> logger, IOptions<BuildOptions> options, IPipelineContext context) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var csproj = context.FileSystem.CurrentDirectory.GetFile(options.Value.ProjectFile);
        if (!csproj.Exists)
        {
            throw new FileNotFoundException("Could not find project file", csproj.AbsolutePath);
        }

        await using var projectStream = csproj.OpenRead();
        var report = new XmlDocument();
        report.Load(projectStream);
        if (report.DocumentElement == null)
        {
            throw new Exception("Could not parse csproj");
        }

        var packageName = report.SelectSingleNode("Project/PropertyGroup/PackageId")?.FirstChild?.Value;
        if (packageName == null)
        {
            throw new Exception("Unable to find PackageId in project file.");
        }

        var packageVersion = report.SelectSingleNode("Project/PropertyGroup/Version")?.FirstChild?.Value;
        if (packageVersion == null)
        {
            throw new Exception("Unable to find Version in project file.");
        }

        var projectInfo = new ProjectInfo
        {
            Name = packageName,
            Version = NuGetVersion.Parse(packageVersion)
        };

        logger.LogInformation("Extracted project info: {ProjectName} (v{Version})", projectInfo.Name, projectInfo.Version);

        context.State.Set(projectInfo);
    }
}
