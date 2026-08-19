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

    [Fact]
    public void WithSetting_Twice_ReplacesInPlace()
    {
        // Act — a key may only appear once; binding would throw on a repeat.
        var statement = SettingsStatement.Database("postgres")
            .WithSetting("connection_string", "Host=localhost")
            .WithSetting("command_timeout", "30")
            .WithSetting("connection_string", "Host=db.internal");

        // Assert — replaced where it was, not appended.
        statement.Settings.Select(setting => setting.Key).ShouldBe(["connection_string", "command_timeout"]);
        statement.Settings[0].Value.ShouldBe("Host=db.internal");
    }

    [Fact]
    public void WithSetting_MatchesTheKeyCaseInsensitively()
    {
        // Act — keys bind case-insensitively, so they collide case-insensitively.
        var statement = SettingsStatement.Database("postgres")
            .WithSetting("connection_string", "Host=localhost")
            .WithSetting("CONNECTION_STRING", "Host=db.internal");

        // Assert
        statement.Settings.ShouldHaveSingleItem().Value.ShouldBe("Host=db.internal");
    }

    [Fact]
    public void WithSettingsFrom_AppliesTheOverlayOverItsOwn()
    {
        // Arrange
        var basis = SettingsStatement.State("s3").WithSetting("bucket", "shared").WithSetting("key", "state.json");
        var overlay = SettingsStatement.State("s3").WithSetting("key", "prod/state.json");

        // Act
        var merged = basis.WithSettingsFrom(overlay);

        // Assert — what the overlay restates wins; what it omits survives.
        merged.Settings.Single(setting => setting.Key == "key").Value.ShouldBe("prod/state.json");
        merged.Settings.Single(setting => setting.Key == "bucket").Value.ShouldBe("shared");
    }
}
