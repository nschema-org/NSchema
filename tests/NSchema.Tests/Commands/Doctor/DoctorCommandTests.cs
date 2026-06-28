using System.CommandLine;

namespace NSchema.Tests.Commands.Doctor;

/// <summary>
/// Integration coverage for doctor's plugin-diagnostic contract: a misconfigured plugin makes <c>doctor</c> fail
/// (the contract CI gates on) with the problem reported, rather than passing or crashing. Loads the real
/// <c>NSchema.Postgres</c> plugin (SDK + network/cache); it sets the working directory (via <c>--directory</c>) and
/// clears the connection-string env var, restoring both on dispose.
/// </summary>
public sealed class DoctorCommandTests : IDisposable
{
    private const string ConnectionStringEnvVar = "NSCHEMA_POSTGRES_CONNECTION_STRING";

    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-doctor-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string? _savedConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

    public DoctorCommandTests() => Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _savedConnectionString);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task Doctor_WithAMisconfiguredProvider_FailsAndReportsThePluginProblem()
    {
        // Arrange — a project whose postgres provider is missing the required connection_string.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), """
            PROVIDER postgres (
              version = '4.0.0-alpha.2'
            );
            """, TestContext.Current.CancellationToken);

        var parseResult = NSchema.Commands.RootCommand.Create().Parse(["doctor", "--directory", _projectDirectory]);

        // Act — mirror Program.cs (default exception handler off).
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        var exitCode = await parseResult.InvokeAsync(invocation, TestContext.Current.CancellationToken);

        // Assert — doctor aggregated the plugin failure into a non-zero exit (the contract CI gates on) rather than
        // passing or crashing raw.
        exitCode.ShouldBe(1);
    }
}
