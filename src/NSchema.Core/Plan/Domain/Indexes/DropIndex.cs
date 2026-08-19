using NSchema.Model;

namespace NSchema.Plan.Domain.Indexes;

/// <summary>
/// Represents the removal of an existing index from a table in the database schema.
/// </summary>
/// <param name="Index">The address of the index.</param>
public sealed record DropIndex(
    MemberAddress Index
) : MigrationAction;
