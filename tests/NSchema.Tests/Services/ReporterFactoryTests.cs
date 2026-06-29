using NSchema.Commands;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class ReporterFactoryTests
{
    private static IConsoleMessenger Create(params string[] args) =>
        ReporterFactory.CreateMessenger(RootCommand.Create().Parse(args));

    [Fact]
    public void Create_DefaultsToTheSpectreMessenger() =>
        Create("plan").ShouldBeOfType<SpectreConsoleMessenger>();

    [Fact]
    public void Create_Json_ReturnsTheJsonMessenger() =>
        Create("plan", "--json").ShouldBeOfType<JsonConsoleMessenger>();

    [Fact]
    public void ResolveVerbosity_Default_IsNormal() =>
        ReporterFactory.ResolveVerbosity(RootCommand.Create().Parse(["plan"])).ShouldBe(Verbosity.Normal);

    [Fact]
    public void ResolveVerbosity_Verbose_IsVerbose() =>
        ReporterFactory.ResolveVerbosity(RootCommand.Create().Parse(["plan", "--verbose"])).ShouldBe(Verbosity.Verbose);

    [Fact]
    public void ResolveVerbosity_Quiet_IsQuiet() =>
        ReporterFactory.ResolveVerbosity(RootCommand.Create().Parse(["plan", "--quiet"])).ShouldBe(Verbosity.Quiet);

    [Fact]
    public void ResolveVerbosity_QuietAndVerboseTogether_Throws()
    {
        var parseResult = RootCommand.Create().Parse(["plan", "--quiet", "--verbose"]);

        var ex = Should.Throw<InvalidOperationException>(() => ReporterFactory.ResolveVerbosity(parseResult));
        ex.Message.ShouldContain("--quiet and --verbose cannot be used together");
    }
}
