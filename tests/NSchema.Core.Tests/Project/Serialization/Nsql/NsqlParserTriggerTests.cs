using NSchema.Model.Triggers;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for standalone <c>CREATE TRIGGER … ON s.t</c>, which (like <c>GRANT</c>) names its table and is
/// attached to it at build time.
/// </summary>
public sealed class NsqlParserTriggerTests
{
    private const string Table = "CREATE SCHEMA app; CREATE TABLE app.users (id int NOT NULL); ";

    private static Trigger ParseTrigger(string triggerSql) =>
        new TestNsqlParser(Table + triggerSql).Parse().Database
            .Schemas.ShouldHaveSingleItem()
            .Tables.ShouldHaveSingleItem()
            .Triggers.ShouldHaveSingleItem();

    [Fact]
    public void Parse_FullTrigger_CapturesEveryClause()
    {
        // Arrange
        var trigger = ParseTrigger(
            "CREATE TRIGGER audit AFTER INSERT OR UPDATE OF (email) ON app.users " +

            // Act
            "FOR EACH ROW WHEN (new.email IS NOT NULL) EXECUTE FUNCTION app.log_change();");

        // Assert
        trigger.Name.ShouldBe("audit");
        trigger.Timing.ShouldBe(TriggerTiming.After);
        trigger.Events.ShouldBe(TriggerEvent.Insert | TriggerEvent.Update);
        trigger.UpdateOfColumns.ShouldBe(["email"]);
        trigger.Level.ShouldBe(TriggerLevel.Row);
        trigger.When.ShouldBe("new.email IS NOT NULL");
        trigger.Function.ShouldBe("app.log_change");
        trigger.FunctionArguments.ShouldBeNull();
    }

    [Fact]
    public void Parse_OmittedForEach_DefaultsToStatement()
        => ParseTrigger("CREATE TRIGGER t BEFORE DELETE ON app.users EXECUTE FUNCTION app.f();")
            .Level.ShouldBe(TriggerLevel.Statement);

    [Fact]
    public void Parse_InsteadOf_ParsesTiming()
        => ParseTrigger("CREATE TRIGGER t INSTEAD OF UPDATE ON app.users FOR EACH ROW EXECUTE FUNCTION app.f();")
            .Timing.ShouldBe(TriggerTiming.InsteadOf);

    [Fact]
    public void Parse_TruncateEvent_ParsesEvent()
        => ParseTrigger("CREATE TRIGGER t AFTER TRUNCATE ON app.users EXECUTE FUNCTION app.f();")
            .Events.ShouldBe(TriggerEvent.Truncate);

    [Fact]
    public void Parse_ExecuteProcedure_IsAccepted()
        => ParseTrigger("CREATE TRIGGER t AFTER INSERT ON app.users EXECUTE PROCEDURE app.f();")
            .Function.ShouldBe("app.f");

    [Fact]
    public void Parse_FunctionArguments_AreCapturedVerbatim()
        => ParseTrigger("CREATE TRIGGER t AFTER INSERT ON app.users FOR EACH ROW EXECUTE FUNCTION app.f('x', 1);")
            .FunctionArguments.ShouldBe("'x', 1");

    [Fact]
    public void Parse_DocComment_AttachesComment()
        => ParseTrigger("--- audit row changes\nCREATE TRIGGER t AFTER INSERT ON app.users EXECUTE FUNCTION app.f();")
            .Comment.ShouldBe("audit row changes");

    [Fact]
    public void Parse_InlineBody_CapturesBodyVerbatim()
    {
        // Arrange
        // The SQL Server form: an inline dollar-quoted body (which may contain its own ';') instead of EXECUTE FUNCTION.
        var trigger = ParseTrigger(

            // Act
            "CREATE TRIGGER audit AFTER INSERT OR DELETE ON app.users AS $$\nBEGIN\n  INSERT INTO app.log VALUES (1);\nEND\n$$;");

        // Assert
        trigger.Name.ShouldBe("audit");
        trigger.Timing.ShouldBe(TriggerTiming.After);
        trigger.Events.ShouldBe(TriggerEvent.Insert | TriggerEvent.Delete);
        trigger.Function.ShouldBeNull();
        trigger.Body.ShouldNotBeNull();
        trigger.Body!.Value.ShouldContain("BEGIN");
        trigger.Body!.Value.ShouldContain("INSERT INTO app.log VALUES (1);");
        trigger.Body!.Value.ShouldContain("END");
    }

    [Fact]
    public void Parse_FunctionTrigger_HasNullBody()
        => ParseTrigger("CREATE TRIGGER t AFTER INSERT ON app.users EXECUTE FUNCTION app.f();")
            .Body.ShouldBeNull();

    [Fact]
    public void Parse_TriggerActionMissing_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestNsqlParser(Table + "CREATE TRIGGER t AFTER INSERT ON app.users;").Parse())
            .Message.ShouldContain("Expected EXECUTE or AS");

    [Fact]
    public void Parse_TriggerOnUnknownTable_FailsTheRead()
        => new TestNsqlParser("CREATE SCHEMA app; CREATE TRIGGER t AFTER INSERT ON app.ghost EXECUTE FUNCTION app.f();").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("unknown table");

    [Fact]
    public void Parse_DuplicateTrigger_FailsTheRead()
        => new TestNsqlParser(Table +
            "CREATE TRIGGER t AFTER INSERT ON app.users EXECUTE FUNCTION app.f(); " +
            "CREATE TRIGGER t AFTER DELETE ON app.users EXECUTE FUNCTION app.f();").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_DuplicateEvent_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestNsqlParser(Table + "CREATE TRIGGER t AFTER INSERT OR INSERT ON app.users EXECUTE FUNCTION app.f();").Parse())
            .Message.ShouldContain("more than once");

    [Fact]
    public void Parse_BadTiming_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestNsqlParser(Table + "CREATE TRIGGER t DURING INSERT ON app.users EXECUTE FUNCTION app.f();").Parse())
            .Message.ShouldContain("Expected BEFORE, AFTER or INSTEAD OF");

    [Fact]
    public void Parse_PartialTrigger_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestNsqlParser("CREATE PARTIAL TRIGGER t AFTER INSERT ON app.users EXECUTE FUNCTION app.f();").Parse())
            .Message.ShouldContain("after CREATE");
}
