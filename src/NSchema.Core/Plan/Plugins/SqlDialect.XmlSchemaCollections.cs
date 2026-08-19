using NSchema.Plan.Domain;
using NSchema.Plan.Domain.XmlSchemaCollections;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of an XML schema collection. Only SQL Server has them, so the default is to report
    /// the declaration rather than to require every dialect to say so itself.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateXmlSchemaCollection(CreateXmlSchemaCollection action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of an XML schema collection.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropXmlSchemaCollection(DropXmlSchemaCollection action) =>
        Unsupported(action);
}
