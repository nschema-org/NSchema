using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Enums;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of an enum type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateEnum(CreateEnum action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of an enum type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropEnum(DropEnum action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the renaming of an enum type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameEnum(RenameEnum action) =>
        Unsupported(action);

    /// <summary>
    /// Renders adding a value to an enum type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddEnumValue(AddEnumValue action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing an enum type's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetEnumComment(SetEnumComment action) =>
        Unsupported(action);
}
