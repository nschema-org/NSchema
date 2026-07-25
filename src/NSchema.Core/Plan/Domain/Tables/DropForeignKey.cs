using NSchema.Model;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the removal of an existing foreign key constraint from a table in the database schema.
/// </summary>
/// <param name="ForeignKey">The address of the foreign key.</param>
public sealed record DropForeignKey(
    MemberAddress ForeignKey
) : MigrationAction;
