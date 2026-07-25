using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents altering the identity sequence options of an existing column in the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldOptions">The current identity sequence options of the column before alteration. This may be null if the column did not previously have identity options.</param>
/// <param name="NewOptions">The new identity sequence options of the column after alteration. This may be null if the column is being altered to remove identity options.</param>
public sealed record AlterIdentitySequence(
    MemberAddress Column,
    IdentityOptions? OldOptions,
    IdentityOptions? NewOptions
) : MigrationAction;
