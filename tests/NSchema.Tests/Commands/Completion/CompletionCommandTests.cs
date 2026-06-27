using System.CommandLine;
using System.CommandLine.Completions;

namespace NSchema.Tests.Commands.Completion;

public sealed class CompletionCommandTests
{
    private readonly RootCommand _sut = NSchema.Commands.RootCommand.Create();

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    [InlineData("pwsh")]
    public void Completion_AcceptsEachKnownShell(string shell)
        => _sut.Parse(["completion", shell]).Errors.ShouldBeEmpty();

    [Fact]
    public void Completion_RejectsAnUnknownShell()
        => _sut.Parse(["completion", "powershell"]).Errors.ShouldNotBeEmpty();

    [Fact]
    public void Completion_RequiresAShellArgument()
        => _sut.Parse(["completion"]).Errors.ShouldNotBeEmpty();

    [Theory]
    [InlineData("install")]
    [InlineData("uninstall")]
    public void Completion_AcceptsInstallAndUninstallSubcommands(string verb)
        => _sut.Parse(["completion", verb, "bash"]).Errors.ShouldBeEmpty();

    [Theory]
    [InlineData("install")]
    [InlineData("uninstall")]
    public void CompletionInstallVerbs_RequireAShell(string verb)
        => _sut.Parse(["completion", verb]).Errors.ShouldNotBeEmpty();

    [Theory]
    [InlineData("install")]
    [InlineData("uninstall")]
    public void CompletionInstallVerbs_RejectAnUnknownShell(string verb)
        => _sut.Parse(["completion", verb, "powershell"]).Errors.ShouldNotBeEmpty();

    [Fact]
    public void Root_RegistersTheSuggestDirective()
        // The shell scripts call `nschema [suggest:…]`; that directive must be wired on the root.
        => _sut.Directives.ShouldContain(directive => directive is SuggestDirective);

    [Fact]
    public void GetCompletions_SurfacesSubcommands()
    {
        // The dynamic data the scripts feed back to the shell — prove a subcommand completion is produced.
        var completions = _sut.Parse("pl").GetCompletions().Select(item => item.Label);

        completions.ShouldContain("plan");
    }
}
