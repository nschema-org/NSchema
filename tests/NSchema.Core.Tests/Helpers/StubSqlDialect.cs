using NSchema.Plan.Backends;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Deterministic stand-in for a dialect: one comment statement per action, whatever its tier, so tests
/// assert orchestration without real SQL (and applying a "plan" of comments to a live database is a no-op).
/// Scripts pass through verbatim, as with a real dialect.
/// </summary>
internal class StubSqlDialect : SqlDialect
{
    private static Result<IReadOnlyList<SqlStatement>> Comment(MigrationAction action) =>
        Statements(new SqlStatement($"-- {action.GetType().Name}"));

    protected override Result<IReadOnlyList<SqlStatement>> CreateSchema(CreateSchema action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropSchema(DropSchema action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameSchema(RenameSchema action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> GrantSchemaUsage(GrantSchemaUsage action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RevokeSchemaUsage(RevokeSchemaUsage action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetSchemaComment(SetSchemaComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateTable(CreateTable action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropTable(DropTable action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameTable(RenameTable action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddPrimaryKey(AddPrimaryKey action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropPrimaryKey(DropPrimaryKey action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddForeignKey(AddForeignKey action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropForeignKey(DropForeignKey action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> GrantTablePrivileges(GrantTablePrivileges action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RevokeTablePrivileges(RevokeTablePrivileges action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetTableComment(SetTableComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> AddColumn(AddColumn action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropColumn(DropColumn action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameColumn(RenameColumn action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterColumn(AlterColumn action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterIdentitySequence(AlterIdentitySequence action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetColumnDefault(SetColumnDefault action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetColumnGenerated(SetColumnGenerated action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetColumnComment(SetColumnComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> AddCheckConstraint(AddCheckConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropCheckConstraint(DropCheckConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddUniqueConstraint(AddUniqueConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropUniqueConstraint(DropUniqueConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddExclusionConstraint(AddExclusionConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropExclusionConstraint(DropExclusionConstraint action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetConstraintComment(SetConstraintComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateIndex(CreateIndex action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropIndex(DropIndex action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetIndexComment(SetIndexComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateTrigger(CreateTrigger action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropTrigger(DropTrigger action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetTriggerComment(SetTriggerComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateView(CreateView action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropView(DropView action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameView(RenameView action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetViewComment(SetViewComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateEnum(CreateEnum action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropEnum(DropEnum action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameEnum(RenameEnum action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddEnumValue(AddEnumValue action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetEnumComment(SetEnumComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateDomain(CreateDomain action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropDomain(DropDomain action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameDomain(RenameDomain action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RecreateDomain(RecreateDomain action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterDomainDefault(AlterDomainDefault action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterDomainNotNull(AlterDomainNotNull action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddDomainCheck(AddDomainCheck action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropDomainCheck(DropDomainCheck action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetDomainComment(SetDomainComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateCompositeType(CreateCompositeType action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropCompositeType(DropCompositeType action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameCompositeType(RenameCompositeType action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AddCompositeField(AddCompositeField action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropCompositeField(DropCompositeField action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterCompositeFieldType(AlterCompositeFieldType action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetCompositeTypeComment(SetCompositeTypeComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateSequence(CreateSequence action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropSequence(DropSequence action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameSequence(RenameSequence action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterSequence(AlterSequence action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetSequenceComment(SetSequenceComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateRoutine(CreateRoutine action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropRoutine(DropRoutine action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RenameRoutine(RenameRoutine action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> RecreateRoutine(RecreateRoutine action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetRoutineComment(SetRoutineComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> CreateExtension(CreateExtension action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> DropExtension(DropExtension action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> AlterExtension(AlterExtension action) => Comment(action);
    protected override Result<IReadOnlyList<SqlStatement>> SetExtensionComment(SetExtensionComment action) => Comment(action);

    protected override Result<IReadOnlyList<SqlStatement>> ExecuteScript(ExecuteScript action) => Statements(action.Statement);
}
