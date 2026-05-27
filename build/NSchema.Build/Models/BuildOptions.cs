namespace NSchema.Build.Models;

public class BuildOptions
{
    public required string ArtifactsDirectory { get; set; }
    public required string TempDirectory { get; set; }
    public required string Configuration { get; set; }
    public required string ProjectFile { get; set; }
    public required string NuGetFeed { get; set; }
    public required string NuGetApiKey { get; set; }
    public bool? IsPreRelease { get; set; }
}
