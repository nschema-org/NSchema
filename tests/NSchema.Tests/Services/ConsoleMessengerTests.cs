using NSchema.Commands;
using NSchema.Services;

namespace NSchema.Tests.Services;

public sealed class ConsoleMessengerTests
{
    private static IConsoleMessenger Create(params string[] args) =>
        ConsoleMessenger.Create(RootCommand.Create().Parse(args));

    [Fact]
    public void Create_DefaultsToTheSpectreMessenger() =>
        Create("plan").ShouldBeOfType<SpectreConsoleMessenger>();

    [Fact]
    public void Create_Json_ReturnsTheJsonMessenger() =>
        Create("plan", "--json").ShouldBeOfType<JsonConsoleMessenger>();

    [Fact]
    public void ResolveVerbosity_Default_IsNormal() =>
        ConsoleMessenger.ResolveVerbosity(RootCommand.Create().Parse(["plan"])).ShouldBe(Verbosity.Normal);

    [Fact]
    public void ResolveVerbosity_Verbose_IsVerbose() =>
        ConsoleMessenger.ResolveVerbosity(RootCommand.Create().Parse(["plan", "--verbose"])).ShouldBe(Verbosity.Verbose);

    [Fact]
    public void ResolveVerbosity_Quiet_IsQuiet() =>
        ConsoleMessenger.ResolveVerbosity(RootCommand.Create().Parse(["plan", "--quiet"])).ShouldBe(Verbosity.Quiet);

    [Fact]
    public void ResolveVerbosity_QuietAndVerboseTogether_Throws()
    {
        var parseResult = RootCommand.Create().Parse(["plan", "--quiet", "--verbose"]);

        var ex = Should.Throw<InvalidOperationException>(() => ConsoleMessenger.ResolveVerbosity(parseResult));
        ex.Message.ShouldContain("--quiet and --verbose cannot be used together");
    }
}
