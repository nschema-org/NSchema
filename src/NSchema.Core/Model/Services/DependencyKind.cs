namespace NSchema.Model.Services;

/// <summary>
/// What sort of thing a <see cref="DependencyNode"/> is.
/// </summary>
internal enum DependencyKind
{
    /// <summary>
    /// A table.
    /// </summary>
    Table,

    /// <summary>
    /// A view.
    /// </summary>
    View,

    /// <summary>
    /// A foreign key constraint on a table.
    /// </summary>
    ForeignKey,

    /// <summary>
    /// An enum type.
    /// </summary>
    Enum,

    /// <summary>
    /// A domain.
    /// </summary>
    Domain,

    /// <summary>
    /// A composite type.
    /// </summary>
    CompositeType,

    /// <summary>
    /// A column on a table.
    /// </summary>
    Column,

    /// <summary>
    /// A routine.
    /// </summary>
    Routine,

    /// <summary>
    /// A captured native type.
    /// </summary>
    NativeType,

    /// <summary>
    /// A database-global extension.
    /// </summary>
    Extension
}
