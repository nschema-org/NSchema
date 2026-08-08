using NSchema.Commands;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class ReporterFactoryTests
{
    private static IConsoleReporter Create(params string[] args) =>
        ReporterFactory.CreateReporter(RootCommand.Create().Parse(args));

    [Fact]
    public void Create_DefaultsToTheSpectreReporter() =>
        Create("plan").ShouldBeOfType<SpectreConsoleReporter>();

    [Fact]
    public void Create_Json_ReturnsTheJsonReporter() =>
        Create("plan", "--json").ShouldBeOfType<JsonConsoleReporter>();

    [Fact]
    public void Create_Markdown_ReturnsTheMarkdownReporter() =>
        Create("plan", "--format", "markdown").ShouldBeOfType<MarkdownConsoleReporter>();

    [Fact]
    public void CreateReporter_Text_ReturnsSpectreReporter() =>
        ReporterFactory.CreateReporter(OutputFormat.Text, Verbosity.Normal).ShouldBeOfType<SpectreConsoleReporter>();

    [Fact]
    public void CreateReporter_Json_ReturnsJsonReporter() =>
        ReporterFactory.CreateReporter(OutputFormat.Json, Verbosity.Normal).ShouldBeOfType<JsonConsoleReporter>();

    [Fact]
    public void CreateReporter_Markdown_ReturnsMarkdownReporter() =>
        ReporterFactory.CreateReporter(OutputFormat.Markdown, Verbosity.Normal).ShouldBeOfType<MarkdownConsoleReporter>();

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
    public void Parse_JsonWithConflictingFormat_IsAUsageError()
    {
        // The conflict is caught while parsing (RootCommand's validator), which is what keeps ResolveFormat total.
        var parseResult = RootCommand.Create().Parse(["plan", "--json", "--format", "markdown"]);

        parseResult.Errors.ShouldContain(error => error.Message.Contains("--json cannot be combined with --format"));
    }

    [Fact]
    public void Parse_JsonWithMatchingFormat_IsAccepted()
    {
        // --json is shorthand for --format json, so agreeing is not a conflict.
        var parseResult = RootCommand.Create().Parse(["plan", "--json", "--format", "json"]);

        parseResult.Errors.ShouldBeEmpty();
        ReporterFactory.ResolveFormat(parseResult).ShouldBe(OutputFormat.Json);
    }

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
    public void Parse_QuietAndVerboseTogether_IsAUsageError()
    {
        var parseResult = RootCommand.Create().Parse(["plan", "--quiet", "--verbose"]);

        parseResult.Errors.ShouldContain(error => error.Message.Contains("--quiet and --verbose cannot be used together"));
    }
}
