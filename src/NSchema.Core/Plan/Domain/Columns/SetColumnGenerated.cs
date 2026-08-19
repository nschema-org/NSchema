using NSchema.Model;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents changing the stored generation expression of an existing column (adding, dropping, or replacing a
/// <c>GENERATED ALWAYS AS (expr) STORED</c> clause).
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldExpression">The current generation expression, or <see langword="null"/> when the column was not generated.</param>
/// <param name="NewExpression">The new generation expression, or <see langword="null"/> when the column becomes a plain column.</param>
public sealed record SetColumnGenerated(
    MemberAddress Column,
    SqlText? OldExpression,
    SqlText? NewExpression
) : MigrationAction;
