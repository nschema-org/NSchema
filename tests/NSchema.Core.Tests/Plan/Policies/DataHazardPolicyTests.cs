using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public class DataHazardPolicyTests
{
    private readonly DataHazardPolicy _sut = new();

    [Fact]
    public void Validate_RequiredColumnAddWithoutDefault_IsFlagged()
    {
        // Arrange — the founding case: ADD COLUMN NOT NULL without a DEFAULT fails against a populated table.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Source.ShouldBe("data-hazards");
        results[0].Message.ShouldContain("app.users.email");
        results[0].Message.ShouldContain("DEFAULT");
    }

    [Fact]
    public void Validate_ReportsHazardsAsWarnings()
    {
        // Arrange — whether a hazard actually fails depends on the data, so it warns rather than blocks.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Validate_RequiredColumnAddOnNewTable_IsNotFlagged()
    {
        // Arrange — an added table is empty at apply time, so nothing in it can fail on data.
        var table = new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }] };
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Tables = [
                TableDiff.Added("app", table) with
                {
                    Columns = [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text })],
                },
            ],
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnAddWithDefault_IsNotFlagged()
    {
        // Arrange — a default gives existing rows their value, so the add cannot fail.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, DefaultExpression = "''" })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullableColumnAdd_IsNotFlagged()
    {
        // Arrange
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, IsNullable = true })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_IdentityColumnAdd_IsNotFlagged()
    {
        // Arrange — an identity column computes its own values for existing rows.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_GeneratedColumnAdd_IsNotFlagged()
    {
        // Arrange — a generated column computes its own values for existing rows.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Added(new Column { Name = "total", Type = SqlType.Int, GeneratedExpression = "a + b" })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnTightenedToNotNull_IsFlagged()
    {
        // Arrange — SET NOT NULL fails if the column holds NULLs.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Modified(new Column { Name = "email", Type = SqlType.Text })
                with { Nullability = new ValueChange<bool>(true, false) }]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("app.users.email");
        results[0].Message.ShouldContain("NOT NULL");
    }

    [Fact]
    public void Validate_ColumnLoosenedToNullable_IsNotFlagged()
    {
        // Arrange — dropping NOT NULL cannot fail on data.
        var diff = ModifiedTable(columns:
            [ColumnDiff.Modified(new Column { Name = "email", Type = SqlType.Text, IsNullable = true })
                with { Nullability = new ValueChange<bool>(false, true) }]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Theory]
    // String and binary narrowing (no length means unbounded).
    [InlineData("varchar(100)", "varchar(50)", true)]
    [InlineData("varchar(50)", "varchar(100)", false)]
    [InlineData("text", "varchar(50)", true)]
    [InlineData("varchar(50)", "text", false)]
    [InlineData("char(10)", "varchar(5)", true)]
    [InlineData("varbinary(100)", "varbinary(50)", true)]
    [InlineData("varbinary(50)", "varbinary(100)", false)]
    // Parsing text into a structured type.
    [InlineData("text", "int", true)]
    [InlineData("varchar(50)", "datetime", true)]
    [InlineData("int", "text", false)]
    // Integer narrowing.
    [InlineData("int", "smallint", true)]
    [InlineData("smallint", "bigint", false)]
    [InlineData("bigint", "int", true)]
    // Decimal capacity.
    [InlineData("decimal(10,2)", "decimal(8,2)", true)]
    [InlineData("decimal(8,2)", "decimal(10,2)", false)]
    [InlineData("decimal(10,2)", "decimal(12,4)", false)] // whole digits unchanged; scale only grows
    [InlineData("int", "decimal(8,2)", true)] // 6 whole digits cannot hold every int
    [InlineData("int", "decimal(12,2)", false)]
    [InlineData("decimal(5,0)", "int", true)]
    // Floats.
    [InlineData("double", "float", true)]
    [InlineData("float", "double", false)]
    [InlineData("double", "bigint", true)]
    [InlineData("int", "double", false)]
    // Unknown types cannot be reasoned about, so they stay silent.
    [InlineData("citext", "varchar(5)", false)]
    [InlineData("text", "citext", false)]
    public void Validate_ColumnTypeChange_FlagsCastsThatCanFail(string oldType, string newType, bool expected)
    {
        // Arrange
        var diff = ModifiedTable(columns:
        [
            ColumnDiff.Modified(new Column { Name = "value", Type = SqlType.Parse(newType) })
                with { Type = new ValueChange<SqlType>(SqlType.Parse(oldType), SqlType.Parse(newType)) },
        ]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.Count.ShouldBe(expected ? 1 : 0);
    }

    [Fact]
    public void Validate_PrimaryKeyAddOverExistingColumns_IsFlagged()
    {
        // Arrange — promoting existing columns to a primary key fails on duplicates or NULLs.
        var diff = ModifiedTable(primaryKey:
            [PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pk", ColumnNames = ["tenant_id", "email"] })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("users_pk");
        results[0].Message.ShouldContain("'tenant_id', 'email'");
    }

    [Fact]
    public void Validate_UniqueConstraintAddOverExistingColumn_IsFlagged()
    {
        // Arrange
        var diff = ModifiedTable(uniqueConstraints:
            [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("users_email_uq");
        results[0].Message.ShouldContain("column 'email'");
    }

    [Fact]
    public void Validate_UniqueConstraintOverColumnsAddedInSameDiff_IsNotFlagged()
    {
        // Arrange — a column added in the same diff starts empty, so uniqueness confined to it cannot collide.
        var diff = ModifiedTable(
            columns: [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, IsNullable = true })],
            uniqueConstraints:
                [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueConstraintRemove_IsNotFlagged()
    {
        // Arrange — dropping uniqueness cannot fail on data (the destructive policy owns that concern).
        var diff = ModifiedTable(uniqueConstraints:
            [UniqueConstraintDiff.Removed("users_email_uq")]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueIndexAddOverExistingColumn_IsFlagged()
    {
        // Arrange
        var diff = ModifiedTable(indexes:
            [IndexDiff.Added(new TableIndex { Name = "ix_users_email", Columns = ["email"], IsUnique = true })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("ix_users_email");
    }

    [Fact]
    public void Validate_UniqueIndexWithExpressionKey_IsFlagged()
    {
        // Arrange — an expression key is opaque, so it is assumed to read pre-existing data.
        var diff = ModifiedTable(indexes:
        [
            IndexDiff.Added(new TableIndex { Name = "ix_users_email", Columns = [new IndexColumn(Expression: "lower(email)")], IsUnique = true }),
        ]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
    }

    [Fact]
    public void Validate_NonUniqueIndexAdd_IsNotFlagged()
    {
        // Arrange — a plain index enforces nothing, so it cannot fail on data.
        var diff = ModifiedTable(indexes:
            [IndexDiff.Added(new TableIndex { Name = "ix_users_email", Columns = ["email"] })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueIndexOverColumnAddedInSameDiff_IsNotFlagged()
    {
        // Arrange
        var diff = ModifiedTable(
            columns: [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text, IsNullable = true })],
            indexes: [IndexDiff.Added(new TableIndex { Name = "ix_users_email", Columns = ["email"], IsUnique = true })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_MultipleHazards_ReturnsOneDiagnosticEach()
    {
        // Arrange
        var diff = ModifiedTable(
            columns: [ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text })],
            uniqueConstraints:
                [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_name_uq", ColumnNames = ["name"] })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Validate_RequiredColumnAddWithMatchedBackfill_IsNotFlagged()
    {
        // Arrange — a matched AddColumn migration backfills the column, so the planner decomposes the add
        // around it and the hazard is handled.
        var diff = ModifiedTable(columns:
        [
            ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text })
            with{
                MigrationScript = Migration(ChangeTrigger.AddColumn, "email"),
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_FailableCastWithMatchedMigration_IsNotFlagged()
    {
        // Arrange — a matched AlterColumnType migration prepares the data before the cast runs.
        var diff = ModifiedTable(columns:
        [
            ColumnDiff.Modified(new Column { Name = "value", Type = SqlType.Int })
                with { Type = new ValueChange<SqlType>(SqlType.Text, SqlType.Int) }
            with {
                MigrationScript = Migration(ChangeTrigger.AlterColumnType, "value"),
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnTightenedToNotNullWithMigration_IsStillFlagged()
    {
        // Arrange — the SET NOT NULL tighten hazard is never silenced by an annotation: the matcher only
        // annotates type changes on modified columns, and the tighten can still fail after the migration.
        var diff = ModifiedTable(columns:
        [
            ColumnDiff.Modified(new Column { Name = "email", Type = SqlType.Int }) with
            {
                Type = new ValueChange<SqlType>(SqlType.Text, SqlType.Int),
                Nullability = new ValueChange<bool>(true, false),
                MigrationScript = Migration(ChangeTrigger.AlterColumnType, "email"),
            },
        ]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert — the cast hazard is suppressed, the NOT NULL tighten is not.
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("NOT NULL");
    }

    [Fact]
    public void Validate_PrimaryKeyAddWithMigration_IsNotFlagged()
    {
        // Arrange — a matched migration declares how the data is de-duplicated/backfilled before the key lands.
        var diff = ModifiedTable(primaryKey:
        [
            PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pk", ColumnNames = ["tenant_id", "email"] })
                with { MigrationScript = Migration(ChangeTrigger.AddConstraint, "users_pk") },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueConstraintAddWithMigration_IsNotFlagged()
    {
        // Arrange
        var diff = ModifiedTable(uniqueConstraints:
        [
            UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })
            with{
                MigrationScript = Migration(ChangeTrigger.AddConstraint, "users_email_uq"),
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueIndexAdd_IsStillFlagged_WhenTableConstraintsCarryMigrations()
    {
        // Arrange — an index is not a constraint: migrations attach to constraint adds, so an annotated
        // constraint on the same table says nothing about the unique index's data.
        var diff = ModifiedTable(
            uniqueConstraints:
            [
                UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })
                with{
                    MigrationScript = Migration(ChangeTrigger.AddConstraint, "users_email_uq"),
                },
            ],
            indexes:
                [IndexDiff.Added(new TableIndex { Name = "ix_users_name", Columns = ["name"], IsUnique = true })]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("ix_users_name");
    }

    // ── Restarting a counter ──────────────────────────────────────────────────
    //
    // Moving a start restarts the live counter, and every value from there on is issued a second time — so an
    // insert collides with a row the table already holds. It discards no object, which is why it is a hazard and
    // not a destructive action, and it matters only where there is data, which is what this policy already knows.

    [Fact]
    public void Validate_MovedIdentityStart_IsFlagged()
    {
        // Arrange
        var diff = ModifiedTable(columns: [IdentityChange(new IdentityOptions(1, null, 1), new IdentityOptions(1000, null, 1))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Source.ShouldBe("data-hazards");
        results[0].Code.ShouldBe("identity-restart-reissues-values");
        results[0].Message.ShouldContain("app.users.id");
        results[0].Message.ShouldContain("1000");
    }

    [Fact]
    public void Validate_CounterRestart_IsAnErrorNotAWarning()
    {
        // Act — every other hazard here fails the migration when the data does not fit, so the engine announces
        // it and a warning is enough. A restart succeeds quietly and hands the damage to the next insert.
        var results = _sut.Validate(
            ModifiedTable(columns: [IdentityChange(new IdentityOptions(1, null, 1), new IdentityOptions(1000, null, 1))]));

        // Assert
        results.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validate_IdentityIncrementChangedButNotTheStart_IsNotFlagged()
        // The case that started this: an increment change must not drag a counter restart along with it.
        => _sut.Validate(ModifiedTable(columns:
                [IdentityChange(new IdentityOptions(null, null, 1), new IdentityOptions(null, null, 2))]))
            .ShouldBeEmpty();

    [Fact]
    public void Validate_MovedSequenceStart_IsFlagged()
    {
        // Arrange — a sequence's consumers are not in the schema, so it is reported against the sequence itself.
        var diff = ModifiedSequence(new SequenceOptions(StartWith: 100), new SequenceOptions(StartWith: 500));

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Code.ShouldBe("sequence-restart-reissues-values");
        results[0].Message.ShouldContain("app.order_id");
    }

    [Fact]
    public void Validate_SequenceStartResetToTheEngineDefault_IsFlagged()
        // A bare RESTART is still a restart: the counter reissues from the default instead of a stated value.
        => _sut.Validate(ModifiedSequence(new SequenceOptions(StartWith: 500), new SequenceOptions()))
            .ShouldHaveSingleItem().Message.ShouldContain("the engine's default");

    [Fact]
    public void Validate_SequenceOptionsChangedButNotTheStart_IsNotFlagged()
        // Every other option moves in place, so a cache change endangers nothing.
        => _sut.Validate(ModifiedSequence(
                new SequenceOptions(StartWith: 100, Cache: 1),
                new SequenceOptions(StartWith: 100, Cache: 50)))
            .ShouldBeEmpty();

    [Fact]
    public void Validate_AddedSequence_IsNotFlagged()
        // A sequence being created has issued nothing yet, so its start endangers nothing — the same reasoning
        // that keeps an added table out of every other hazard here.
        => _sut.Validate(new DatabaseDiff([
                SchemaDiff.Containing("app") with
                {
                    Sequences = [SequenceDiff.Added("app", new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 500) })],
                },
            ])).ShouldBeEmpty();

    private static ColumnDiff IdentityChange(IdentityOptions before, IdentityOptions after) =>
        ColumnDiff.Modified(new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true }) with
        {
            Identity = new ValueChange<IdentityOptions>(before, after),
        };

    private static DatabaseDiff ModifiedSequence(SequenceOptions before, SequenceOptions after) =>
        new([SchemaDiff.Containing("app") with
        {
            Sequences = [SequenceDiff.Modified("app", "order_id") with { Options = new ValueChange<SequenceOptions>(before, after) }],
        }]);

    private static ChangeScript Migration(ChangeTrigger trigger, string member) =>
        new(member, "UPDATE app.users SET email = ''", new ChangeTarget("app", "users", member, trigger));

    private static DatabaseDiff ModifiedTable(
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? primaryKey = null,
        IReadOnlyList<UniqueConstraintDiff>? uniqueConstraints = null) =>
        new([
            SchemaDiff.Containing("app") with
            {
                Tables = [
                TableDiff.Modified("app", "users") with
                {
                    Columns = columns ?? [],
                    Indexes = indexes ?? [],
                    PrimaryKeys = primaryKey ?? [],
                    UniqueConstraints = uniqueConstraints ?? [],
                },
            ],
            },
        ]);
}
