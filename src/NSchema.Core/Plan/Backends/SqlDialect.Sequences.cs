using System.Text;
using NSchema.Model.Sequences;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Sequences;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a sequence. <see cref="AnsiCreateSequence"/> is the standard form for
    /// dialects opting in.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateSequence(CreateSequence action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a sequence. <see cref="AnsiDropSequence"/> is the standard form for
    /// dialects opting in.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropSequence(DropSequence action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the renaming of a sequence.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameSequence(RenameSequence action) =>
        Unsupported(action);

    /// <summary>
    /// Renders altering a sequence's options. <see cref="AnsiAlterSequence"/> is the standard form for
    /// dialects opting in.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AlterSequence(AlterSequence action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a sequence's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetSequenceComment(SetSequenceComment action) =>
        Unsupported(action);

    // ── ANSI builders ─────────────────────────────────────────────────────────
    // Sequences are ANSI-standard but not universal, so no default renders one silently; a dialect whose
    // engine has them opts in by delegating to these.

    /// <summary>
    /// The standard <c>CREATE SEQUENCE</c> form of <paramref name="action"/>.
    /// </summary>
    protected Result<IReadOnlyList<SqlStatement>> AnsiCreateSequence(CreateSequence action)
    {
        var sql = new StringBuilder($"CREATE SEQUENCE {Qualify(action.SchemaName, action.Sequence.Name)}");
        AppendSequenceOptions(sql, action.Sequence.Options);
        return Statement(sql.ToString());
    }

    /// <summary>
    /// The standard <c>ALTER SEQUENCE</c> form of <paramref name="action"/>.
    /// </summary>
    protected Result<IReadOnlyList<SqlStatement>> AnsiAlterSequence(AlterSequence action)
    {
        var sql = new StringBuilder($"ALTER SEQUENCE {Qualify(action.Sequence)}");
        AppendSequenceOptions(sql, action.NewOptions);
        return Statement(sql.ToString());
    }

    /// <summary>
    /// The standard <c>DROP SEQUENCE</c> form of <paramref name="action"/>.
    /// </summary>
    protected Result<IReadOnlyList<SqlStatement>> AnsiDropSequence(DropSequence action) =>
        Statement($"DROP SEQUENCE {Qualify(action.Sequence)}");

    private static void AppendSequenceOptions(StringBuilder sql, SequenceOptions options)
    {
        if (options.DataType is not null)
        {
            sql.Append($" AS {options.DataType}");
        }

        if (options.StartWith is { } start)
        {
            sql.Append($" START WITH {start}");
        }

        if (options.IncrementBy is { } increment)
        {
            sql.Append($" INCREMENT BY {increment}");
        }

        if (options.MinValue is { } min)
        {
            sql.Append($" MINVALUE {min}");
        }

        if (options.MaxValue is { } max)
        {
            sql.Append($" MAXVALUE {max}");
        }

        if (options.Cache is { } cache)
        {
            sql.Append($" CACHE {cache}");
        }

        if (options.Cycle)
        {
            sql.Append(" CYCLE");
        }
    }
}
