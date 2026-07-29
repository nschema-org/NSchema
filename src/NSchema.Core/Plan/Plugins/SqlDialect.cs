using NSchema.Model;
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

namespace NSchema.Plan.Plugins;

/// <summary>
/// The SQL dialect a database provider plugs in.
/// </summary>
/// <remarks>
/// Override methods to provide an implementation for a given action. Mandatory implementations are abstract, optional ones are virtual.
/// Use the <see cref="Statement"/>, <see cref="Statements"/>, <see cref="Quote"/>, <see cref="Unsupported"/> and <see cref="Skipped"/>
/// methods for easy return values and consistent diagnostics.
/// </remarks>
public abstract partial class SqlDialect
{
    /// <summary>
    /// The dialect's display name, used in its diagnostics.
    /// </summary>
    protected virtual string Name => GetType().Name;

    /// <summary>
    /// Whether a foreign key can be added to, or dropped from, a table that already exists.
    /// </summary>
    /// <remarks>
    /// A dialect that says no keeps every foreign key on the <see cref="CreateTable"/> that declares it, and the
    /// plan never separates one from its table — so the database must accept a key naming a table created later
    /// in the run, and must not need one cut to drop the tables it joins. SQLite is the case this exists for.
    /// </remarks>
    public virtual bool CanAlterForeignKeys => true;

    /// <summary>
    /// Renders <paramref name="action"/> as the SQL statement(s) that perform it.
    /// </summary>
    /// <param name="action">The migration action to render.</param>
    /// <returns>The ordered statements performing the action, or the diagnostics explaining why it cannot be rendered.</returns>
    public Result<IReadOnlyList<SqlStatement>> Generate(MigrationAction action) => action switch
    {
        // Schemas
        CreateSchema x => CreateSchema(x),
        DropSchema x => DropSchema(x),
        RenameSchema x => RenameSchema(x),
        GrantSchemaUsage x => GrantSchemaUsage(x),
        RevokeSchemaUsage x => RevokeSchemaUsage(x),
        SetSchemaComment x => SetSchemaComment(x),

        // Tables
        CreateTable x => CreateTable(x),
        DropTable x => DropTable(x),
        RenameTable x => RenameTable(x),
        AddPrimaryKey x => AddPrimaryKey(x),
        DropPrimaryKey x => DropPrimaryKey(x),
        AddForeignKey x => AddForeignKey(x),
        DropForeignKey x => DropForeignKey(x),
        GrantTablePrivileges x => GrantTablePrivileges(x),
        RevokeTablePrivileges x => RevokeTablePrivileges(x),
        SetTableComment x => SetTableComment(x),

        // Columns
        AddColumn x => AddColumn(x),
        DropColumn x => DropColumn(x),
        RenameColumn x => RenameColumn(x),
        AlterColumn x => AlterColumn(x),
        AlterIdentitySequence x => AlterIdentitySequence(x),
        SetColumnDefault x => SetColumnDefault(x),
        SetColumnGenerated x => SetColumnGenerated(x),
        SetColumnComment x => SetColumnComment(x),

        // Constraints
        AddCheckConstraint x => AddCheckConstraint(x),
        DropCheckConstraint x => DropCheckConstraint(x),
        AddUniqueConstraint x => AddUniqueConstraint(x),
        DropUniqueConstraint x => DropUniqueConstraint(x),
        AddExclusionConstraint x => AddExclusionConstraint(x),
        DropExclusionConstraint x => DropExclusionConstraint(x),
        SetConstraintComment x => SetConstraintComment(x),

        // Indexes
        CreateIndex x => CreateIndex(x),
        DropIndex x => DropIndex(x),
        SetIndexComment x => SetIndexComment(x),

        // Triggers
        CreateTrigger x => CreateTrigger(x),
        DropTrigger x => DropTrigger(x),
        SetTriggerComment x => SetTriggerComment(x),

        // Views
        CreateView x => CreateView(x),
        DropView x => DropView(x),
        RenameView x => RenameView(x),
        SetViewComment x => SetViewComment(x),

        // Enums
        CreateEnum x => CreateEnum(x),
        DropEnum x => DropEnum(x),
        RenameEnum x => RenameEnum(x),
        AddEnumValue x => AddEnumValue(x),
        SetEnumComment x => SetEnumComment(x),

        // Domains
        CreateDomain x => CreateDomain(x),
        DropDomain x => DropDomain(x),
        RenameDomain x => RenameDomain(x),
        RecreateDomain x => RecreateDomain(x),
        AlterDomainDefault x => AlterDomainDefault(x),
        AlterDomainNotNull x => AlterDomainNotNull(x),
        AddDomainCheck x => AddDomainCheck(x),
        DropDomainCheck x => DropDomainCheck(x),
        SetDomainComment x => SetDomainComment(x),

        // Composite types
        CreateCompositeType x => CreateCompositeType(x),
        DropCompositeType x => DropCompositeType(x),
        RenameCompositeType x => RenameCompositeType(x),
        AddCompositeField x => AddCompositeField(x),
        DropCompositeField x => DropCompositeField(x),
        AlterCompositeFieldType x => AlterCompositeFieldType(x),
        SetCompositeTypeComment x => SetCompositeTypeComment(x),

        // Sequences
        CreateSequence x => CreateSequence(x),
        DropSequence x => DropSequence(x),
        RenameSequence x => RenameSequence(x),
        AlterSequence x => AlterSequence(x),
        SetSequenceComment x => SetSequenceComment(x),

        // Routines
        CreateRoutine x => CreateRoutine(x),
        DropRoutine x => DropRoutine(x),
        RenameRoutine x => RenameRoutine(x),
        RecreateRoutine x => RecreateRoutine(x),
        SetRoutineComment x => SetRoutineComment(x),

        // Extensions
        CreateExtension x => CreateExtension(x),
        DropExtension x => DropExtension(x),
        AlterExtension x => AlterExtension(x),
        SetExtensionComment x => SetExtensionComment(x),

        // Scripts
        ExecuteScript x => ExecuteScript(x),

        _ => Result.Failure<IReadOnlyList<SqlStatement>>(SqlDialectDiagnostics.Unknown(action, Name)),
    };

    // ── Formatting kernel ─────────────────────────────────────────────────────

    /// <summary>
    /// Quotes a single identifier. The base form is the ANSI double-quoted identifier.
    /// </summary>
    protected virtual string Quote(SqlIdentifier identifier) => $"\"{identifier.Value.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// Renders a schema-qualified object name.
    /// </summary>
    protected virtual string Qualify(SqlIdentifier schema, SqlIdentifier name) => $"{Quote(schema)}.{Quote(name)}";

    /// <summary>
    /// Renders an object address as its quoted, qualified name.
    /// </summary>
    protected string Qualify(ObjectAddress address) => Qualify(address.Schema, address.Name);

    /// <summary>
    /// Renders a comma-separated list of quoted identifiers.
    /// </summary>
    protected string ColumnList(IEnumerable<SqlIdentifier> columns) => string.Join(", ", columns.Select(Quote));

    // ── Rendering outcomes ────────────────────────────────────────────────────

    /// <summary>
    /// A successful single-statement rendering.
    /// </summary>
    protected static Result<IReadOnlyList<SqlStatement>> Statement(string sql, bool runOutsideTransaction = false) =>
        Result.Success<IReadOnlyList<SqlStatement>>([new SqlStatement(sql, runOutsideTransaction)]);

    /// <summary>
    /// A successful rendering of the given statements.
    /// </summary>
    protected static Result<IReadOnlyList<SqlStatement>> Statements(params SqlStatement[] statements) =>
        Result.Success<IReadOnlyList<SqlStatement>>(statements);

    /// <summary>
    /// The failed rendering of an action this dialect cannot execute: an error diagnostic that blocks the plan.
    /// </summary>
    protected Result<IReadOnlyList<SqlStatement>> Unsupported(MigrationAction action) =>
        Result.Failure<IReadOnlyList<SqlStatement>>(SqlDialectDiagnostics.Unsupported(action, Name));

    /// <summary>
    /// The empty rendering of an action this dialect deliberately ignores: a warning diagnostic, and the plan
    /// proceeds without the change (which therefore reappears in every future plan until it leaves the project).
    /// </summary>
    protected Result<IReadOnlyList<SqlStatement>> Skipped(MigrationAction action) =>
        Result.Success<IReadOnlyList<SqlStatement>>([], SqlDialectDiagnostics.Skipped(action, Name));
}
