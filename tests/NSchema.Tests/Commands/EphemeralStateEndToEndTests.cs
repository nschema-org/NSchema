using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using NSchema.Project.Nsql;
using Spectre.Console;

namespace NSchema.Tests.Commands;

/// <summary>
/// End-to-end coverage of <c>--ephemeral</c>: the CI bootstrap workflow where a disposable database is
/// planned and applied with no <c>STATE</c> store configured. Loads the real published <c>NSchema.Sqlite</c>
/// plugin (SDK + network/cache) and uses its own sample schema, so the whole path — config resolution, plugin
/// load, plan, confirmation bypass, execution, ephemeral state capture — runs for real against a throwaway
/// SQLite database. It sets the working directory (via <c>--directory</c>), restoring it on dispose.
/// </summary>
/// <remarks>
/// Every run's console output is captured and attached to the assertions, because the CLI reports an expected
/// failure as rendered diagnostics and a non-zero exit code — so a bare "expected 0, was 1" would hide the one
/// thing that explains the break.
/// </remarks>
public sealed class EphemeralEndToEndTests : IDisposable
{
    /// <summary>
    /// Rendered width for the captured consoles. Spectre cannot size a non-terminal writer, and the CLI's own
    /// redirected fallback is deliberately enormous — which would pad every diagnostics table to that width and
    /// bury the message it is being captured for.
    /// </summary>
    private const string CaptureWidth = "200";

    private static SemanticVersion SqliteVersion => PublishedPlugins.Sqlite;

    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-ephemeral-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly IAnsiConsole _originalConsole = AnsiConsole.Console;
    private readonly TextWriter _originalError = Console.Error;
    private readonly string? _originalColumns = Environment.GetEnvironmentVariable(EnvironmentVariables.Columns);

    public EphemeralEndToEndTests() => Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, CaptureWidth);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, _originalColumns);
        AnsiConsole.Console = _originalConsole;
        Console.SetError(_originalError);
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task PlanAndApply_WithEphemeral_BootstrapADisposableDatabase()
    {
        // Arrange — a project declaring a DATABASE but no STATE; the schema is the plugin's own sample.
        var plugin = new PluginLoader(Directory.GetCurrentDirectory()).Load(new PackageId("NSchema.Sqlite"), SqliteVersion)
            .Require()
            .OfType<INSchemaDatabasePlugin>()
            .Single();

        var databasePath = Path.Combine(_projectDirectory, "app.db");
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), $"""
            PLUGIN sqlite (
              source  = 'NSchema.Sqlite',
              version = '{SqliteVersion}'
            );

            DATABASE sqlite ( connection_string = 'Data Source={databasePath}' );
            """, TestContext.Current.CancellationToken);
        await LockFileManager.Write(ProjectConfigurationReader.LockFilePath(_projectDirectory),
            new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Sqlite"), Version = SqliteVersion }]), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "schemas"));
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "schemas", "example.sql"),
            NsqlWriter.Write(plugin.GetSampleSchema()), TestContext.Current.CancellationToken);

        // Act — plan, then apply, each standing the ephemeral store in for a state backend.
        var plan = await Invoke("plan", "--ephemeral");
        var apply = await Invoke("apply", "--ephemeral", "--auto-approve");

        // Assert — both runs succeeded and the apply actually created the database.
        plan.ExitCode.ShouldBe(0, plan.Transcript);
        apply.ExitCode.ShouldBe(0, apply.Transcript);
        File.Exists(databasePath).ShouldBeTrue(apply.Transcript);
    }

    /// <summary>
    /// Runs one CLI invocation against the project directory with its console output captured.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>Program.cs</c> — the ambient Spectre console it installs, and its disabled default exception
    /// handler — but over writers instead of the real streams. Both streams are intercepted because the text
    /// reporters split by severity: narration and results go to the ambient console, while warnings, errors and
    /// any diagnostics table go to a console the messenger builds over <see cref="Console.Error"/>.
    /// </remarks>
    private async Task<CliRun> Invoke(params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        AnsiConsole.Console = ConsoleFactory.Create(output, colorDisabled: true);
        Console.SetError(error);

        var exitCode = await NSchema.Commands.RootCommand.Create()
            .Parse([.. arguments, "--directory", _projectDirectory])
            .InvokeAsync(
                new InvocationConfiguration { EnableDefaultExceptionHandler = false, Output = output, Error = error },
                TestContext.Current.CancellationToken);

        return new CliRun(string.Join(' ', arguments), exitCode, output.ToString(), error.ToString());
    }

    /// <summary>One CLI invocation's exit code and everything it printed.</summary>
    private sealed record CliRun(string Arguments, int ExitCode, string Output, string Error)
    {
        /// <summary>What the run printed, labelled by stream — the assertion message when the exit code is wrong.</summary>
        public string Transcript => $"""
            'nschema {Arguments}' exited with {ExitCode}.
            ---- stdout ----
            {Output.Trim()}
            ---- stderr ----
            {Error.Trim()}
            ----------------
            """;
    }
}
