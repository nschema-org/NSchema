using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Publications;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a publication.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreatePublication(CreatePublication action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a publication.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropPublication(DropPublication action) =>
        Unsupported(action);

    /// <summary>
    /// Renders renaming a publication.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenamePublication(RenamePublication action) =>
        Unsupported(action);

    /// <summary>
    /// Renders adding a table to a publication.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddPublicationTable(AddPublicationTable action) =>
        Unsupported(action);

    /// <summary>
    /// Renders removing a table from a publication.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropPublicationTable(DropPublicationTable action) =>
        Unsupported(action);

    /// <summary>
    /// Renders publishing a schema's tables.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddPublicationSchema(AddPublicationSchema action) =>
        Unsupported(action);

    /// <summary>
    /// Renders no longer publishing a schema's tables.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropPublicationSchema(DropPublicationSchema action) =>
        Unsupported(action);

    /// <summary>
    /// Renders changing the row changes a publication publishes.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetPublicationOperations(SetPublicationOperations action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a publication's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetPublicationComment(SetPublicationComment action) =>
        Unsupported(action);
}
