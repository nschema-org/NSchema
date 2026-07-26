using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Building a configuration statement without touching the token layer: a factory per keyword, refined with the
/// <c>With…</c> methods. What a plugin uses to render its own <c>DATABASE</c> or <c>STATE</c> statement.
/// </summary>
public sealed class SettingsStatementTests
{
    private static string Write(SettingsStatement statement) => NsqlWriter.Write(new NsqlDocument([statement]));

    [Fact]
    public void Database_CarriesItsLabelAndSettings()
    {
        // Act
        var statement = SettingsStatement.Database("postgres")
            .WithSetting("connection_string", "Host=localhost")
            .WithSetting("command_timeout", "30");

        // Assert
        Write(statement).ShouldBe(
            """
            DATABASE postgres (
              connection_string = 'Host=localhost',
              command_timeout = '30'
            );

            """);
    }

    [Fact]
    public void Engine_TakesNoLabel()
    {
        // Act
        var statement = SettingsStatement.Engine().WithSetting("version", "[5.0,6.0)");

        // Assert
        statement.Label.ShouldBeNull();
        Write(statement).ShouldBe(
            """
            ENGINE (
              version = '[5.0,6.0)'
            );

            """);
    }

    [Fact]
    public void WithSetting_AppendsInOrder_LeavingTheOriginalAlone()
    {
        // Arrange — each link returns a copy, so a statement can be shared as a starting point.
        var basis = SettingsStatement.State("s3").WithSetting("bucket", "state");

        // Act
        var extended = basis.WithSetting("key", "nschema.state.json");

        // Assert
        basis.Settings.Select(setting => setting.Key).ShouldBe(["bucket"]);
        extended.Settings.Select(setting => setting.Key).ShouldBe(["bucket", "key"]);
    }

    [Fact]
    public void WithDocComment_LeadsTheStatement()
    {
        // Act
        var statement = SettingsStatement.Database("sqlite")
            .WithSetting("connection_string", "Data Source=app.db")
            .WithDocComment("A local SQLite database file.");

        // Assert — a doc-comment is '---', which the language reads as the catalog comment for what follows.
        Write(statement).ShouldStartWith("--- A local SQLite database file.\nDATABASE sqlite (");
    }

    [Fact]
    public void WithLeadingComment_SitsAboveTheStatement_SeparatedByABlankLine()
    {
        // Act
        var statement = SettingsStatement.Engine()
            .WithSetting("version", "[5.0,6.0)")
            .WithLeadingComment("-- Project configuration.");

        // Assert — an ordinary '--' comment introducing what follows, not a doc-comment attached to it.
        Write(statement).ShouldStartWith("-- Project configuration.\n\nENGINE (");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LeadingCommentAndDocComment_ComposeInEitherOrder(bool documentFirst)
    {
        // Arrange
        var basis = SettingsStatement.State("s3").WithSetting("bucket", "state");

        // Act — the chaining order must not decide which comment ends up on top.
        var statement = documentFirst
            ? basis.WithDocComment("Credentials come from the AWS chain.").WithLeadingComment("-- Overlay for prod.")
            : basis.WithLeadingComment("-- Overlay for prod.").WithDocComment("Credentials come from the AWS chain.");

        // Assert
        Write(statement).ShouldStartWith(
            """
            -- Overlay for prod.

            --- Credentials come from the AWS chain.
            STATE s3 (
            """);
    }

    [Fact]
    public void AMultiLineComment_KeepsItsLines()
    {
        // Act
        var statement = SettingsStatement.Engine()
            .WithSetting("version", "[5.0,6.0)")
            .WithLeadingComment("-- First line.\n-- Second line.");

        // Assert
        Write(statement).ShouldStartWith("-- First line.\n-- Second line.\n\nENGINE (");
    }

    [Fact]
    public void AStatementItBuilds_RoundTripsThroughTheReader()
    {
        // Arrange
        var statement = SettingsStatement.Database("postgres").WithSetting("connection_string", "Host=localhost");

        // Act
        var document = NsqlReader.Read(Write(statement));

        // Assert
        var read = document.Require().Statements.OfType<SettingsStatement>().ShouldHaveSingleItem();
        read.Keyword.ShouldBe(SettingsKeyword.Database);
        read.Label!.Value.ShouldBe("postgres");
        read.Settings.ShouldHaveSingleItem().Value.ShouldBe("Host=localhost");
    }
}
