using NSchema.Plugins;
using NSchema.Services.Prompting;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

/// <summary>
/// How a plugin's scaffolding questions get answered: from <c>--set</c> where supplied, from the operator where a
/// terminal allows, and from the question's own default otherwise.
/// </summary>
public sealed class ScaffoldPrompterTests
{
    private static readonly ScaffoldPrompt Host = new() { Key = "host", Label = "Host", Default = "localhost" };
    private static readonly ScaffoldPrompt Database = new() { Key = "database", Label = "Database" };

    private static Dictionary<string, string?> Supplied(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase);

    private static TestConsole Interactive() => new TestConsole().Interactive();

    [Fact]
    public void SuppliedAnswer_SkipsThePrompt()
    {
        // Arrange — a supplied answer must win even on a terminal, so a scripted run never blocks.
        var console = Interactive();

        // Act
        var answers = ScaffoldPrompter.Answer(console, [Host], Supplied(("host", "db.internal")));

        // Assert
        answers["host"].ShouldBe("db.internal");
        console.Output.ShouldBeEmpty();
    }

    [Fact]
    public void WithoutATerminal_OptionalQuestionsTakeTheirDefault()
    {
        // Arrange
        var console = new TestConsole();

        // Act
        var answers = ScaffoldPrompter.Answer(console, [Host], Supplied());

        // Assert
        answers["host"].ShouldBe("localhost");
    }

    [Fact]
    public void WithoutATerminal_ARequiredQuestionFailsNamingIt()
    {
        // Arrange — scaffolding a half-configured project would surface the problem later, further from the cause.
        var console = new TestConsole();

        // Act
        var act = () => ScaffoldPrompter.Answer(console, [Host, Database], Supplied());

        // Assert
        var error = act.ShouldThrow<ScaffoldAnswerRequiredException>();
        error.Message.ShouldContain("database");
        error.Message.ShouldContain("--set");
    }

    [Fact]
    public void WithoutATerminal_ARequiredQuestionIsSatisfiedBySet()
    {
        // Arrange
        var console = new TestConsole();

        // Act
        var answers = ScaffoldPrompter.Answer(console, [Host, Database], Supplied(("database", "orders")));

        // Assert — the optional one still falls back, the required one is answered.
        answers["host"].ShouldBe("localhost");
        answers["database"].ShouldBe("orders");
    }

    [Fact]
    public void SuppliedAnswer_MayItselfContainSeparators()
    {
        // Arrange — a connection string is the common answer and is full of '=' and ';'.
        var console = new TestConsole();
        var connection = "Host=db.internal;Port=5432;Database=app";

        // Act
        var answers = ScaffoldPrompter.Answer(
            console,
            [new ScaffoldPrompt { Key = "connection_string", Label = "Connection string" }],
            Supplied(("connection_string", connection)));

        // Assert
        answers["connection_string"].ShouldBe(connection);
    }

    [Fact]
    public void OnATerminal_TheOperatorIsAsked()
    {
        // Arrange
        var console = Interactive();
        console.Input.PushTextWithEnter("orders");

        // Act
        var answers = ScaffoldPrompter.Answer(console, [Database], Supplied());

        // Assert
        answers["database"].ShouldBe("orders");
        console.Output.ShouldContain("Database");
    }
}
