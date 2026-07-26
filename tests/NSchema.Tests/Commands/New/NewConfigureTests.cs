using NSchema.Commands.New;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;
using Spectre.Console.Testing;

namespace NSchema.Tests.Commands.New;

/// <summary>
/// The scaffold's half of the plugin contract: what a plugin asks for gets asked, and the answers reach the template
/// it renders. Uses a stub plugin rather than a published one — what is under test is the wiring, not any provider.
/// </summary>
public sealed class NewConfigureTests
{
    /// <summary>A plugin that asks for the parts of a connection string and composes them, as a real one does.</summary>
    private sealed class StubPlugin : INSchemaDatabasePlugin
    {
        public IReadOnlyList<ScaffoldPrompt> GetScaffoldPrompts(ScaffoldContext context) =>
        [
            new() { Key = "host", Label = "Host", Default = "localhost" },
            new() { Key = "database", Label = "Database", Default = "postgres" },
        ];

        public SettingsStatement GetScaffoldTemplate(ScaffoldContext context) =>
            new(SettingsKeyword.Database, Identifier.Synthetic("stub"), new SeparatedSyntaxList<Setting>(
            [
                new Setting("connection_string", $"Host={context.Answer("host", "?")};Database={context.Answer("database", "?")}"),
            ]));

        public string GetSampleSchema() => "CREATE TABLE main.t (id bigint NOT NULL);";

        public Result Configure(NSchemaApplicationBuilder builder, PluginSettings settings) => Result.Success();
    }

    /// <summary>A plugin that scaffolds fixed placeholders, as the ones written before prompts existed do.</summary>
    private sealed class SilentPlugin : INSchemaDatabasePlugin
    {
        public SettingsStatement GetScaffoldTemplate(ScaffoldContext context) =>
            new(SettingsKeyword.Database, Identifier.Synthetic("silent"), new SeparatedSyntaxList<Setting>([]));

        public string GetSampleSchema() => string.Empty;

        public Result Configure(NSchemaApplicationBuilder builder, PluginSettings settings) => Result.Success();
    }

    private static Dictionary<string, string?> Supplied(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Configure_SuppliedAnswers_ReachTheRenderedStatement()
    {
        // Arrange
        var console = new TestConsole();
        var plugin = new StubPlugin();

        // Act
        var context = NewCommand.Configure(
            console, plugin, new ScaffoldContext(), Supplied(("host", "db.internal"), ("database", "orders")));

        // Assert
        NewCommand.Render(plugin.GetScaffoldTemplate(context))
            .ShouldContain("Host=db.internal;Database=orders");
    }

    [Fact]
    public void Configure_Unanswered_FallsBackToTheDeclaredDefaults()
    {
        // Arrange — no terminal, nothing supplied: the plugin's own defaults are what gets written.
        var console = new TestConsole();
        var plugin = new StubPlugin();

        // Act
        var context = NewCommand.Configure(console, plugin, new ScaffoldContext(), Supplied());

        // Assert
        NewCommand.Render(plugin.GetScaffoldTemplate(context))
            .ShouldContain("Host=localhost;Database=postgres");
    }

    [Fact]
    public void Configure_PluginAsksNothing_LeavesTheContextAlone()
    {
        // Arrange — a plugin written before prompts existed must be unaffected.
        var console = new TestConsole();
        var context = new ScaffoldContext { EnvironmentName = "prod" };

        // Act
        var configured = NewCommand.Configure(console, new SilentPlugin(), context, Supplied(("host", "ignored")));

        // Assert
        configured.ShouldBeSameAs(context);
        configured.Answers.ShouldBeEmpty();
    }

    [Fact]
    public void Configure_AnswersSurviveAnEnvironmentOverlay()
    {
        // Arrange — the questions are put once; the overlay varies only by environment.
        var console = new TestConsole();
        var plugin = new StubPlugin();

        // Act
        var context = NewCommand.Configure(console, plugin, new ScaffoldContext(), Supplied(("host", "db.internal")));
        var overlay = context with { EnvironmentName = "prod" };

        // Assert
        NewCommand.Render(plugin.GetScaffoldTemplate(overlay)).ShouldContain("Host=db.internal");
    }
}
