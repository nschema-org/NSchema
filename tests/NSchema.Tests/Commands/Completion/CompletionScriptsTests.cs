using NSchema.Commands.Completion;

namespace NSchema.Tests.Commands.Completion;

public sealed class CompletionScriptsTests
{
    [Theory]
    [InlineData("bash", "complete -f -F _nschema_complete nschema")]
    [InlineData("zsh", "compdef _nschema_complete nschema")]
    [InlineData("fish", "complete -c nschema")]
    [InlineData("pwsh", "Register-ArgumentCompleter -Native -CommandName nschema")]
    public void For_KnownShell_RegistersAndCallsTheSuggestBackend(string shell, string registration)
    {
        var script = CompletionScripts.For(shell);

        // Every script must (a) register a completer for nschema and (b) drive it off the `[suggest]` directive.
        script.ShouldContain(registration);
        script.ShouldContain("[suggest:");
    }

    [Fact]
    public void For_EveryAdvertisedShell_HasAScript()
    {
        foreach (var shell in CompletionCommand.Shells)
        {
            CompletionScripts.For(shell).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void For_UnknownShell_Throws()
        => Should.Throw<ArgumentOutOfRangeException>(() => CompletionScripts.For("powershell"));
}
