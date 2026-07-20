using System.CommandLine;
using NSchema.Configuration.Plugins;
using NSchema.Extensions;
using NSchema.Plugins;

namespace NSchema.Tests.Commands;

/// <summary>
/// End-to-end coverage of <c>--ephemeral</c>: the CI bootstrap workflow where a disposable database is
/// planned and applied with no <c>STATE</c> store configured. Loads the real published <c>NSchema.Sqlite</c>
/// plugin (SDK + network/cache) and uses its own sample schema, so the whole path — config resolution, plugin
/// load, plan, confirmation bypass, execution, ephemeral state capture — runs for real against a throwaway
/// SQLite database. It sets the working directory (via <c>--directory</c>), restoring it on dispose.
/// </summary>
public sealed class EphemeralEndToEndTests : IDisposable
{
    private const string Version = "5.0.0-alpha.1";

    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-ephemeral-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task PlanAndApply_WithEphemeral_BootstrapADisposableDatabase()
    {
        // Arrange — a project declaring a DATABASE but no STATE; the schema is the plugin's own sample.
        var plugin = new PluginLoader().Load("NSchema.Sqlite", Version)
            .Require()
            .OfType<INSchemaDatabasePlugin>()
            .Single();

        var databasePath = Path.Combine(_projectDirectory, "app.db");
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.env.sql"), $"""
            PLUGIN sqlite (
              source  = 'NSchema.Sqlite',
              version = '{Version}'
            );

            DATABASE sqlite ( connection_string = 'Data Source={databasePath}' );
            """, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "schemas"));
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "schemas", "example.sql"),
            plugin.GetSampleSchema(), TestContext.Current.CancellationToken);

        // Act — mirror Program.cs (default exception handler off): plan, then apply, each standing the
        // ephemeral store in for a state backend.
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        var planExit = await NSchema.Commands.RootCommand.Create()
            .Parse(["plan", "--ephemeral", "--directory", _projectDirectory])
            .InvokeAsync(invocation, TestContext.Current.CancellationToken);
        var applyExit = await NSchema.Commands.RootCommand.Create()
            .Parse(["apply", "--ephemeral", "--auto-approve", "--directory", _projectDirectory])
            .InvokeAsync(invocation, TestContext.Current.CancellationToken);

        // Assert — both runs succeeded and the apply actually created the database.
        planExit.ShouldBe(0);
        applyExit.ShouldBe(0);
        File.Exists(databasePath).ShouldBeTrue();
    }
}
