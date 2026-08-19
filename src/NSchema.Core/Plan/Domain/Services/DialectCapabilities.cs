using NSchema.Plan.Plugins;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// What the target database can do, where the answer changes the shape of a plan rather than the text of a statement.
/// Read from the dialect once, so the stages that decide the shape never hold the one that renders.
/// </summary>
/// <param name="CanAlterForeignKeys">Whether a foreign key can be added to, or dropped from, an existing table.</param>
internal readonly record struct DialectCapabilities(bool CanAlterForeignKeys)
{
    /// <summary>
    /// What standard SQL allows, for a caller with no dialect to ask.
    /// </summary>
    public static DialectCapabilities Standard { get; } = new(CanAlterForeignKeys: true);

    /// <summary>
    /// What <paramref name="dialect"/> says it can do.
    /// </summary>
    public static DialectCapabilities Of(SqlDialect dialect) => new(dialect.CanAlterForeignKeys);
}
