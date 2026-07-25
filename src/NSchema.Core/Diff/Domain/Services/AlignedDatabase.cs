using NSchema.Model;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// The current side of a compare, rewritten with the desired/declared names.
/// </summary>
/// <param name="Database">The current schema, with every applied rename directive already rewritten.</param>
/// <param name="Renames">What the alignment renamed.</param>
internal sealed record AlignedDatabase(Database Database, RenameLog Renames)
{
    /// <summary>
    /// A current side with no renames to apply — what drift and teardown compare under.
    /// </summary>
    public static AlignedDatabase Unaligned(Database database) => new(database, RenameLog.Empty);
}
