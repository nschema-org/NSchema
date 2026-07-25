using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Services;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="PlanLinearizer"/>'s handling of diff nodes annotated with a matched change-event
/// script: the backfill decomposition of a required column add, the ordering of
/// <see cref="ExecuteScript"/> around type changes and constraint adds, and the script flow-through.
/// </summary>
public sealed class PlanLinearizerDataMigrationTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> LinearizeTable(TableDiff table)
        => _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [table] }]));

    private IReadOnlyList<MigrationAction> LinearizeColumn(ColumnDiff column)
        => LinearizeTable(TableDiff.Modified("app", "users") with { Columns = [column] });

    private static ChangeScript Migration(ChangeTrigger trigger, string member, string? name = null, string? sql = null) =>
        new(name ?? member, sql ?? $"UPDATE app.users -- {member}", new ChangeTarget("app", "users", member, trigger));

    [Fact]
    public void Linearize_AnnotatedRequiredColumnAdd_DecomposesIntoNullableAddBackfillAndTighten()
    {
        // Arrange — a NOT NULL, no-default column with a matched backfill cannot land in one step against a
        // populated table: it is added nullable, backfilled, then tightened.
        var migration = Migration(ChangeTrigger.AddColumn, "email", name: "backfill_emails");
        var column = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(3);
        var add = plan[0].ShouldBeOfType<AddColumn>();
        add.Column.Name.ShouldBe("email");
        add.Column.IsNullable.ShouldBeTrue();
        var backfill = plan[1].ShouldBeOfType<ExecuteScript>();
        backfill.Script.Name.ShouldBe("backfill_emails");
        backfill.Script.Sql.ShouldBe(migration.Sql);
        var tighten = plan[2].ShouldBeOfType<AlterColumn>();
        tighten.Column.Name.ShouldBe("email");
        tighten.Nullability.ShouldBe(new ValueChange<bool>(true, false));
        tighten.Column.Type.ShouldBe(SqlType.Text);
    }

    [Fact]
    public void Linearize_AnnotatedNullableColumnAdd_EmitsPlainAddPlusMigration()
    {
        // Arrange — a nullable add needs no decomposition: the column lands as declared, then the migration runs.
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var column = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, IsNullable = true }) with { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.IsNullable.ShouldBeTrue();
        plan[1].ShouldBeOfType<ExecuteScript>();
        plan.OfType<AlterColumn>().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("defaulted")]
    [InlineData("identity")]
    [InlineData("generated")]
    public void Linearize_AnnotatedSelfFillingRequiredAdd_IsNotDecomposed(string shape)
    {
        // Arrange — defaults, identity, and generation fill existing rows themselves, so the add keeps its
        // declared NOT NULL shape and only the migration is appended.
        var definition = shape switch
        {
            "defaulted" => new Column { Name = "email", Type = SqlType.Text, DefaultExpression = "''" },
            "identity" => new Column { Name = "email", Type = SqlType.BigInt, IsIdentity = true },
            _ => new Column { Name = "email", Type = SqlType.Text, GeneratedExpression = "lower(name)" },
        };
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var column = ColumnDiff.Added(definition) with { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.ShouldBe(definition);
        plan[1].ShouldBeOfType<ExecuteScript>();
        plan.OfType<AlterColumn>().ShouldBeEmpty();
    }

    [Fact]
    public void Linearize_AnnotatedTypeChange_RunsMigrationBeforeAlterColumn()
    {
        // Arrange — the migration's SQL prepares the data for the cast, so it must run first.
        var migration = Migration(ChangeTrigger.AlterColumnType, "total");
        var column = ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.Int }) with
        {
            Type = new ValueChange<SqlType>(SqlType.Text, SqlType.Int),
            MigrationScript = migration,
        };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        var prep = plan[0].ShouldBeOfType<ExecuteScript>();
        prep.Script.ShouldBe(migration);
        var alter = plan[1].ShouldBeOfType<AlterColumn>();
        alter.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.Int));
    }

    [Fact]
    public void Linearize_AnnotatedUniqueConstraintAdd_RunsMigrationBeforeAddUniqueConstraint()
    {
        // Arrange — the migration de-duplicates the data the constraint depends on.
        var migration = Migration(ChangeTrigger.AddConstraint, "users_email_uq");
        var constraint = UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })
        with
        { MigrationScript = migration };

        // Act
        var plan = LinearizeTable(TableDiff.Modified("app", "users") with { UniqueConstraints = [constraint] });

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<ExecuteScript>().Script.ShouldBe(migration);
        plan[1].ShouldBeOfType<AddUniqueConstraint>().UniqueConstraint.Name.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_TwoAnnotatedChanges_KeepMigrationsInDiffOrder()
    {
        // Arrange — two annotated column adds; their migrations share a priority band, so the stable sort
        // preserves the diff's declaration order.
        var first = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, IsNullable = true })
        with
        {
            MigrationScript = Migration(ChangeTrigger.AddColumn, "email", name: "first"),
        };
        var second = ColumnDiff.Added(new Column { Name = "phone", Type = SqlType.Text, IsNullable = true })
        with
        {
            MigrationScript = Migration(ChangeTrigger.AddColumn, "phone", name: "second"),
        };

        // Act
        var plan = LinearizeTable(TableDiff.Modified("app", "users") with { Columns = [first, second] });

        // Assert
        plan.OfType<ExecuteScript>().Select(m => m.Script.Name).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Linearize_MatchedScript_RidesTheActionWhole()
    {
        // Arrange
        var migration = new ChangeScript("dedupe", "DELETE FROM app.users",
            new ChangeTarget("app", "users", "users_pk", ChangeTrigger.AddConstraint))
        {
            RunOutsideTransaction = true,
        };
        var constraint = PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pk", ColumnNames = ["id"] })
            with
        { MigrationScript = migration };

        // Act
        var plan = LinearizeTable(TableDiff.Modified("app", "users") with { PrimaryKeys = [constraint] });

        // Assert — the action carries the declared script itself, nothing copied field-by-field.
        plan.OfType<ExecuteScript>().ShouldHaveSingleItem().Script.ShouldBe(migration);
    }
}
