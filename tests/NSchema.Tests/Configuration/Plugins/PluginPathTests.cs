using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// Resolving a plugin declared by path: where the path is anchored, what makes one unusable, and what a project
/// that declares one may no longer claim about itself.
/// </summary>
public sealed class PluginPathTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-plugin-path-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    // A plugin is only loadable if its dependency closure sits beside it, which is what .deps.json describes.
    // Nothing here loads the assembly, so the bytes are irrelevant — only whether the files are where they must be.
    private string Plant(string name = "NSchema.Postgres", bool withDependencies = true, string? subdirectory = null)
    {
        var directory = subdirectory is null ? _directory : Path.Combine(_directory, subdirectory);
        Directory.CreateDirectory(directory);

        var assembly = Path.Combine(directory, $"{name}.dll");
        File.WriteAllText(assembly, string.Empty);

        if (withDependencies)
        {
            File.WriteAllText(Path.ChangeExtension(assembly, ".deps.json"), "{}");
        }

        return assembly;
    }

    private async Task<Result<ProjectConfiguration>> Read(string plugin)
    {
        var config = $"""
            {plugin}
            DATABASE db ( connection_string = 'Host=localhost' );
            STATE file ( path = './nschema.state.json' );
            """;

        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), config, TestContext.Current.CancellationToken);
        return await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RelativePath_IsResolvedAgainstTheProjectRoot()
    {
        // Not the working directory: a project is run from anywhere, and a path that moved with the shell would
        // mean the same configuration named different bits depending on where it was invoked.

        // Arrange
        var planted = Plant(subdirectory: "artifacts");

        // Act
        var result = await Read("PLUGIN db ( path = './artifacts/NSchema.Postgres.dll' );");

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        result.Value.Database!.Origin.ShouldBeOfType<ResolvedPath>().AssemblyPath.ShouldBe(planted);
    }

    [Fact]
    public async Task Path_IsReportedOnEveryRun()
    {
        // The finding rides on the resolved configuration rather than on the load, so it reaches every command
        // that reads the project: a log has to show that a run used a build rather than a release. Information
        // rather than a warning — it reports how the run was configured, not a fault in the project.

        // Arrange
        Plant();

        // Act
        var result = await Read("PLUGIN db ( path = './NSchema.Postgres.dll' );");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var reported = result.Diagnostics.ShouldHaveSingleItem();
        reported.Code.ShouldBe("plugin-from-path");
        reported.Severity.ShouldBe(DiagnosticSeverity.Info);
    }

    [Fact]
    public async Task Path_IsNeverLocked()
    {
        // Nothing pins the bits behind a path, so writing one into the lockfile would claim a reproducibility it
        // cannot offer — and a later run would read it back as though it meant something.

        // Arrange
        Plant();

        // Act
        var result = await Read("PLUGIN db ( path = './NSchema.Postgres.dll' );");

        // Assert
        result.Value!.ResolvedPlugins().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingAssembly_IsRejected()
    {
        // Act — nothing planted
        var result = await Read("PLUGIN db ( path = './NSchema.Postgres.dll' );");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "plugin-path-not-found");
    }

    [Fact]
    public async Task AssemblyWithoutItsClosure_IsRejectedWithTheCause()
    {
        // A plugin built without CopyLocalLockFileAssemblies leaves the assembly on disk and none of its
        // dependencies.
        // The runtime's own account of that names a component nobody wrote, so the likely cause is named here
        // while there is still something useful to say about it.

        // Arrange
        Plant(withDependencies: false);

        // Act
        var result = await Read("PLUGIN db ( path = './NSchema.Postgres.dll' );");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "plugin-path-not-self-contained");
        result.Diagnostics.ShouldContain(d => d.Message.Contains("CopyLocalLockFileAssemblies"));
    }

    [Fact]
    public async Task FileNameThatIsNotAnAssemblyName_IsRejected()
    {
        // The file name is what the load context is asked for once the closure is in place, so a path naming
        // something else would fail deep in the loader rather than here.

        // Arrange
        Plant(name: "not a package id");

        // Act
        var result = await Read("PLUGIN db ( path = './not a package id.dll' );");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "unusable-plugin-path");
    }
}
