using NSchema.Diff.Domain;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Diff.Domain.XmlSchemaCollections;
using NSchema.Model;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Views;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Views;
using NSchema.Plan.Domain.XmlSchemaCollections;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public class DestructiveActionPolicyTests
{
    private readonly DestructiveActionPolicy _sut = new();

    [Fact]
    public void Validate_DestructiveAction_IsReportedAsOneError()
    {
        // Act — losing something is not recoverable, so the finding is an error; downgrading it is enforcement's job.
        var errors = _sut.Validate(TestData.DestructiveDiff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Severity.ShouldBe(DiagnosticSeverity.Error);
        errors[0].Source.ShouldBe("destructive-actions");
        errors[0].Code.ShouldBe("destructive-change");
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_NonDestructiveAction_ReportsNothing()
    {
        // Act
        var results = _sut.Validate(TestData.NonDestructiveDiff).ToList();

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsOneErrorPerDestructiveActionType()
    {
        // Arrange
        var diff = TestData.DiffWithDroppedTables("users", "accounts");

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Validate_DroppedUniqueConstraint_IsDestructive()
    {
        // Arrange — dropping a unique constraint removes a structural guarantee (and a possible FK target).
        var diff = TableChange(TableDiff.Modified("app", "users") with
        {
            Columns = [],
            Grants = [],
            Indexes = [],
            UniqueConstraints = [UniqueConstraintDiff.Removed("users_email_uq")],
        });

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropUniqueConstraint));
    }

    [Fact]
    public void Validate_DroppedExclusionConstraint_IsDestructive()
    {
        // Arrange — dropping an exclusion constraint removes a structural guarantee, like a unique constraint.
        var diff = TableChange(TableDiff.Modified("app", "bookings") with
        {
            Columns = [],
            Grants = [],
            Indexes = [],
            ExclusionConstraints = [ExclusionConstraintDiff.Removed("no_overlap")],
        });

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropExclusionConstraint));
    }

    [Fact]
    public void Validate_DroppedCheckConstraint_IsNotDestructive()
    {
        // Arrange — dropping a check only loosens validation; no data is lost, so it is not destructive.
        var diff = TableChange(TableDiff.Modified("app", "users") with
        {
            Columns = [],
            Grants = [],
            Indexes = [],
            Checks = [CheckConstraintDiff.Removed("users_age_chk")],
        });

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedView_IsDestructive()
    {
        // Arrange — dropping a view is destructive (its definition is lost from managed state).
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Grants = [],
                Tables = [],
                Views = [ViewDiff.Removed("app", "active_users")],
            },
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropView));
    }

    [Fact]
    public void Validate_AddedView_IsNotDestructive()
    {
        // Arrange — creating a view loses nothing.
        var view = new View { Name = "active_users", Body = "SELECT * FROM app.users" };
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Grants = [],
                Tables = [],
                Views = [ViewDiff.Added("app", view)],
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedEnum_IsDestructive()
    {
        // Arrange — dropping an enum is destructive (columns using it would lose their type definition).
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with { Enums = [EnumDiff.Removed("app", "status")] },
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropEnum));
    }

    [Fact]
    public void Validate_DroppedSequence_IsDestructive()
    {
        // Arrange — dropping a sequence loses its current position.
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with { Sequences = [SequenceDiff.Removed("app", "order_id")] },
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropSequence));
    }

    [Fact]
    public void Validate_AddedEnumAndSequence_AreNotDestructive()
    {
        // Arrange — creating an enum or sequence loses nothing.
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Enums = [EnumDiff.Added("app", new EnumType { Name = "status", Values = ["a"] })],
                Sequences = [SequenceDiff.Added("app", new Sequence { Name = "order_id" })],
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedRoutines_AreDestructive()
    {
        // Arrange — dropping a routine loses its definition from managed state.
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Routines = [
                RoutineDiff.Removed("app", "f", RoutineKind.Function),
                RoutineDiff.Removed("app", "p", RoutineKind.Procedure),
            ],
            },
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropRoutine));
    }

    [Fact]
    public void Validate_FunctionSignatureRecreate_IsNotDestructive()
    {
        // Arrange — a signature change is a declared edit; the database blocks the underlying drop loudly if
        // dependents exist, so the policy does not gate it.
        var fn = new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "a int, b text", Definition = "RETURNS int AS $$ SELECT 1 $$" };
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Routines = [
                RoutineDiff.Modified("app", "f", RoutineKind.Function) with
                {
                    Definition = fn,
                    Arguments = new ValueChange<SqlText>("a int", "a int, b text"),
                },
            ],
            },
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedExtension_IsDestructive()
    {
        // Arrange — dropping a database-global extension removes shared infrastructure (and its dependents).
        var diff = new DatabaseDiff(Extensions: [ExtensionDiff.Removed("citext")]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropExtension));
    }

    [Fact]
    public void Validate_AddedExtension_IsNotDestructive()
    {
        // Arrange — installing an extension loses nothing.
        var diff = new DatabaseDiff(Extensions:
            [ExtensionDiff.Added(new Extension { Name = "citext" })]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    /// <summary>
    /// The policy switches over every schema-object diff kind and throws on one it does not recognise, so a
    /// kind added to <see cref="SchemaDiff.EnumerateObjects"/> without a case here crashes the plan instead of
    /// misjudging it. Walking every kind makes the next one fail here rather than at teardown.
    /// </summary>
    [Fact]
    public void Validate_RemovalOfAnySchemaObjectKind_IsClassifiedRatherThanThrowing()
    {
        // Arrange — one removed object of every kind a schema diff can carry.
        var diff = new DatabaseDiff([SchemaDiff.Containing("app") with
        {
            Grants = [],
            Tables = [TableDiff.Removed("app", "users")],
            Views = [ViewDiff.Removed("app", "active_users")],
            Enums = [EnumDiff.Removed("app", "status")],
            Sequences = [SequenceDiff.Removed("app", "user_id")],
            Routines = [RoutineDiff.Removed("app", "touch", RoutineKind.Procedure)],
            Domains = [DomainDiff.Removed("app", "email")],
            CompositeTypes = [CompositeTypeDiff.Removed("app", "address")],
            XmlSchemaCollections = [XmlSchemaCollectionDiff.Removed("app", "survey")],
        }]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert — one grouped finding, naming a drop action for every kind.
        var message = errors.ShouldHaveSingleItem().Message;
        foreach (var action in new[]
                 {
                     nameof(DropTable), nameof(DropView), nameof(DropEnum), nameof(DropSequence),
                     nameof(DropRoutine), nameof(DropDomain), nameof(DropCompositeType), nameof(DropXmlSchemaCollection),
                 })
        {
            message.ShouldContain(action);
        }
    }

    private static DatabaseDiff TableChange(TableDiff table) =>
        new([SchemaDiff.Containing("app") with
        {
            Grants = [],
            Tables = [table],
        }]);
}
