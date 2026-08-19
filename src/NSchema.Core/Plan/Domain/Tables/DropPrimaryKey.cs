using NSchema.Model;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the removal of an existing primary key constraint from a table in the database schema.
/// </summary>
/// <param name="PrimaryKey">The address of the primary key.</param>
public sealed record DropPrimaryKey(
    MemberAddress PrimaryKey
) : MigrationAction;
