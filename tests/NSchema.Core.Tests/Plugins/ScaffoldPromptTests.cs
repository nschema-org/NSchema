using NSchema.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Plugins;

/// <summary>
/// The scaffolding contract: a plugin declares what to ask, and builds its statement from the answers. Core owns the
/// vocabulary, the front-end owns the asking, and neither knows what the other's questions mean.
/// </summary>
public sealed class ScaffoldPromptTests
{
    /// <summary>
    /// A plugin whose questions are deliberately not its settings: it asks for the parts of a connection and composes
    /// the one setting its statement carries.
    /// </summary>
    private sealed class ComposingPlugin : INSchemaPlugin
    {
        public IReadOnlyList<ScaffoldPrompt> GetScaffoldPrompts(ScaffoldContext context) =>
        [
            new() { Key = "host", Label = "Host", Default = "localhost" },
            new() { Key = "database", Label = "Database" },
            new() { Key = "password", Label = "Password", IsSecret = true },
        ];

        public NsqlDocument GetScaffoldTemplate(ScaffoldContext context) =>
            new([
                SettingsStatement.Database("demo").WithSetting(
                    "connection_string",
                    $"Host={context.Answer("host", "localhost")};Database={context.Answer("database", "app")}"),
            ]);
    }

    [Fact]
    public void Prompt_WithoutADefault_IsRequired()
    {
        // Arrange
        var prompts = new ComposingPlugin().GetScaffoldPrompts(new ScaffoldContext());

        // Act
        var required = prompts.Where(prompt => prompt.IsRequired).Select(prompt => prompt.Key);

        // Assert — a default is what makes a question skippable, so the two cannot disagree.
        required.ShouldBe(["database", "password"]);
    }

    [Fact]
    public void Answers_ComposeIntoTheStatement()
    {
        // Arrange
        var context = new ScaffoldContext
        {
            Answers = new Dictionary<string, string?> { ["host"] = "db.internal", ["database"] = "orders" },
        };

        // Act
        var statement = Configured(new ComposingPlugin().GetScaffoldTemplate(context));

        // Assert — three questions became one setting; that composition is the plugin's alone.
        statement.Settings.ShouldHaveSingleItem().Value.ShouldBe("Host=db.internal;Database=orders");
    }

    [Fact]
    public void UnansweredPrompts_FallBackToPlaceholders()
    {
        // Arrange — a non-interactive run answers nothing, and must still scaffold something editable.
        var context = new ScaffoldContext();

        // Act
        var statement = Configured(new ComposingPlugin().GetScaffoldTemplate(context));

        // Assert
        statement.Settings.ShouldHaveSingleItem().Value.ShouldBe("Host=localhost;Database=app");
    }

    [Fact]
    public void PluginWithNoQuestions_AsksNothing()
    {
        // Arrange — a front-end holds the interface, which is where the default lives.
        INSchemaPlugin plugin = new SilentPlugin();

        // Act
        var prompts = plugin.GetScaffoldPrompts(new ScaffoldContext());

        // Assert — declaring prompts is opt-in; a plugin that scaffolds placeholders implements nothing extra.
        prompts.ShouldBeEmpty();
    }

    [Fact]
    public void ScaffoldedStatement_RendersAsValidNsql()
    {
        // Arrange
        var context = new ScaffoldContext
        {
            Answers = new Dictionary<string, string?> { ["host"] = "db.internal", ["database"] = "orders" },
        };
        var statement = Configured(new ComposingPlugin().GetScaffoldTemplate(context));

        // Act — what the front-end will write out, read straight back.
        var rendered = NsqlWriter.Write(new NsqlDocument([statement]));
        var reparsed = NsqlReader.Read(rendered);

        // Assert
        reparsed.IsSuccess.ShouldBeTrue();
        reparsed.Value.Statements.ShouldHaveSingleItem().ShouldBeOfType<SettingsStatement>()
            .Settings.ShouldHaveSingleItem().Value.ShouldBe("Host=db.internal;Database=orders");
    }

    private static SettingsStatement Configured(NsqlDocument document) =>
        document.Statements.OfType<SettingsStatement>().ShouldHaveSingleItem();

    private sealed class SilentPlugin : INSchemaPlugin
    {
        public NsqlDocument GetScaffoldTemplate(ScaffoldContext context) =>
            new([SettingsStatement.State("file").WithSetting("path", "./nschema.state.json")]);
    }
}
