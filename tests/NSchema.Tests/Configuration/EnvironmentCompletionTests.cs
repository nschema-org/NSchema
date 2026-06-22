using System.CommandLine;

namespace NSchema.Tests.Configuration;

/// <summary>
/// The <c>--environment</c> option completes dynamically from the project's <c>*.env.&lt;name&gt;.sql</c> overlays,
/// resolved against the <c>--directory</c> on the command line (not a fixed value set).
/// </summary>
public sealed class EnvironmentCompletionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-env-completion-").FullName;
    private readonly RootCommand _sut = NSchema.Commands.RootCommand.Create();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relativePath) => File.WriteAllText(Path.Combine(_root, relativePath), "-- placeholder");

    private List<string> CompleteEnvironment(string commandLine) =>
        _sut.Parse(commandLine).GetCompletions().Select(item => item.Label).ToList();

    [Fact]
    public void Environment_CompletesWithOverlayNames_FromTheGivenDirectory()
    {
        // Arrange
        Write("schema.sql");                 // not an overlay — must not be offered
        Write("audit.env.prod.sql");
        Write("seed.env.prod.sql");          // a second prod overlay — name de-duplicated
        Write("scratch.env.dev.sql");

        // Act
        var completions = CompleteEnvironment($"plan --directory {_root} --environment ");

        // Assert
        completions.ShouldBe(["dev", "prod"]);
    }

    [Fact]
    public void Environment_FiltersCandidates_ByTheWordBeingCompleted()
    {
        // Arrange
        Write("audit.env.prod.sql");
        Write("scratch.env.dev.sql");

        // Act
        var completions = CompleteEnvironment($"plan --directory {_root} --environment p");

        // Assert
        completions.ShouldBe(["prod"]);
    }

    [Fact]
    public void Environment_CompletesViaShortAlias()
    {
        // Arrange
        Write("scratch.env.dev.sql");

        // Act
        var completions = CompleteEnvironment($"plan --directory {_root} -e ");

        // Assert
        completions.ShouldBe(["dev"]);
    }
}
