using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Commands.Doctor;

/// <summary>
/// Integration coverage for doctor's plugin-diagnostic contract: a misconfigured plugin makes <c>doctor</c> fail
/// (the contract CI gates on) with the problem reported, rather than passing or crashing. Loads the real
/// <c>NSchema.Postgres</c> plugin (SDK + network/cache); it sets the working directory (via <c>--directory</c>),
/// restoring it on dispose.
/// </summary>
public sealed class DoctorCommandTests : IDisposable
{
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-doctor-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task Doctor_WithAMisconfiguredProvider_FailsAndReportsThePluginProblem()
    {
        // Arrange — a project whose postgres provider is missing the required connection_string.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), """
            PLUGIN postgres (
              source  = 'NSchema.Postgres',
              version = '5.0.0-beta.4'
            );

            DATABASE postgres ();
            """, TestContext.Current.CancellationToken);
        await LockFileManager.Write(ProjectConfigurationReader.LockFilePath(_projectDirectory),
            new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.0.0-beta.4") }]), TestContext.Current.CancellationToken);

        var parseResult = NSchema.Commands.RootCommand.Create().Parse(["doctor", "--directory", _projectDirectory]);

        // Act — mirror Program.cs (default exception handler off).
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        var exitCode = await parseResult.InvokeAsync(invocation, TestContext.Current.CancellationToken);

        // Assert — doctor aggregated the plugin failure into a non-zero exit (the contract CI gates on) rather than
        // passing or crashing raw.
        exitCode.ShouldBe(1);
    }
}
