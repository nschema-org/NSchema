using NSchema.Plan.Domain;
using NSchema.Plan.Domain.CompositeTypes;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a composite type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateCompositeType(CreateCompositeType action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a composite type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropCompositeType(DropCompositeType action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the renaming of a composite type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameCompositeType(RenameCompositeType action) =>
        Unsupported(action);

    /// <summary>
    /// Renders adding a field to a composite type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddCompositeField(AddCompositeField action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping a field from a composite type.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropCompositeField(DropCompositeField action) =>
        Unsupported(action);

    /// <summary>
    /// Renders changing the type of a composite type's field.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AlterCompositeFieldType(AlterCompositeFieldType action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a composite type's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetCompositeTypeComment(SetCompositeTypeComment action) =>
        Unsupported(action);
}
