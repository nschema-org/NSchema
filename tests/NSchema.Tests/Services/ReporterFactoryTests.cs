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
    public void Create_Markdown_ReturnsTheSpectreMessenger_SoNarrationGoesToStderr() =>
        Create("plan", "--format", "markdown").ShouldBeOfType<SpectreConsoleMessenger>();

    private static OutputFormat ResolveFormat(params string[] args) =>
        ReporterFactory.ResolveFormat(RootCommand.Create().Parse(args));

    [Fact]
    public void ResolveFormat_Default_IsText() => ResolveFormat("plan").ShouldBe(OutputFormat.Text);

    [Fact]
    public void ResolveFormat_Json_IsJson() => ResolveFormat("plan", "--json").ShouldBe(OutputFormat.Json);

    [Fact]
    public void ResolveFormat_FormatJson_IsJson() => ResolveFormat("plan", "--format", "json").ShouldBe(OutputFormat.Json);

    [Fact]
    public void ResolveFormat_FormatMarkdown_IsMarkdown() => ResolveFormat("plan", "--format", "markdown").ShouldBe(OutputFormat.Markdown);

    [Fact]
    public void ResolveFormat_JsonWithAgreeingFormat_IsJson() => ResolveFormat("plan", "--json", "--format", "json").ShouldBe(OutputFormat.Json);

    [Fact]
    public void ResolveFormat_JsonWithConflictingFormat_Throws()
    {
        var parseResult = RootCommand.Create().Parse(["plan", "--json", "--format", "markdown"]);

        var ex = Should.Throw<InvalidOperationException>(() => ReporterFactory.ResolveFormat(parseResult));
        ex.Message.ShouldContain("--json cannot be combined with --format");
    }

    [Fact]
    public void CreatePresenter_Text_ReturnsSpectrePresenter() =>
        ReporterFactory.CreatePresenter(OutputFormat.Text).ShouldBeOfType<SpectreConsolePresenter>();

    [Fact]
    public void CreatePresenter_Json_ReturnsJsonPresenter() =>
        ReporterFactory.CreatePresenter(OutputFormat.Json).ShouldBeOfType<JsonConsolePresenter>();

    [Fact]
    public void CreatePresenter_Markdown_ReturnsMarkdownPresenter() =>
        ReporterFactory.CreatePresenter(OutputFormat.Markdown).ShouldBeOfType<MarkdownConsolePresenter>();

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
