using NSchema.Model;

namespace NSchema.Project.Policies;

/// <summary>
/// The diagnostics minted by <see cref="StructuralIntegrityPolicy"/>.
/// </summary>
internal static class StructuralIntegrityDiagnostics
{
    internal static readonly DiagnosticSource Source = "structural-integrity";

    /// <summary>
    /// An index name reused within a schema, where index and index-backed constraint names are scoped.
    /// </summary>
    public static Diagnostic DuplicateIndexName(DatabaseAddress schema, SqlIdentifier name, string sites) =>
        Diagnostic.Error(Source, "duplicate-index-name", $"Schema '{schema}' declares the index name '{name}' more than once ({sites:text}).");

    /// <summary>
    /// An object name declared more than once for the same kind.
    /// </summary>
    public static Diagnostic DuplicateObjectName(ObjectAddress name, string kind) =>
        Diagnostic.Error(Source, "duplicate-object-name", $"Schema '{name.Schema}' declares {kind:text} '{name.Name}' more than once.");

    /// <summary>
    /// An object name reused across kinds that share a name space.
    /// </summary>
    public static Diagnostic CollidingObjectName(ObjectAddress name) =>
        Diagnostic.Error(Source, "colliding-object-name", $"Schema '{name.Schema}' reuses the name '{name.Name}'.");

    /// <summary>
    /// A routine name declared more than once, where functions and procedures share one name space.
    /// </summary>
    public static Diagnostic DuplicateRoutineName(ObjectAddress routine) =>
        Diagnostic.Error(Source, "duplicate-routine-name", $"Schema '{routine.Schema}' declares routine '{routine.Name}' more than once.");

    /// <summary>
    /// A table declared with no columns.
    /// </summary>
    public static Diagnostic EmptyTable(ObjectAddress table) =>
        Diagnostic.Error(Source, "empty-table", $"Table '{table}' has no columns.");

    /// <summary>
    /// A column declared more than once on a table.
    /// </summary>
    public static Diagnostic DuplicateColumn(MemberAddress column) =>
        Diagnostic.Error(Source, "duplicate-column", $"Table '{column.Owner}' declares column '{column.Member}' more than once.");

    /// <summary>
    /// A column carrying both a default and a generated expression, which the database rejects.
    /// </summary>
    public static Diagnostic DefaultOnGeneratedColumn(MemberAddress column) =>
        Diagnostic.Error(Source, "default-on-generated-column", $"Column '{column}' has both a DEFAULT and a GENERATED expression; a generated column cannot have a default.");

    /// <summary>
    /// A primary key referencing a column the table does not declare.
    /// </summary>
    public static Diagnostic UnknownPrimaryKeyColumn(MemberAddress primaryKey, SqlIdentifier column) =>
        Diagnostic.Error(Source, "unknown-primary-key-column", $"Primary key '{primaryKey.Member}' on '{primaryKey.Owner}' references unknown column '{column}'.");

    /// <summary>
    /// An index referencing a column the table does not declare.
    /// </summary>
    public static Diagnostic UnknownIndexColumn(MemberAddress index, SqlIdentifier column) =>
        Diagnostic.Error(Source, "unknown-index-column", $"Index '{index.Member}' on '{index.Owner}' references unknown column '{column}'.");

    /// <summary>
    /// A foreign key referencing a local column the table does not declare.
    /// </summary>
    public static Diagnostic UnknownLocalColumn(MemberAddress foreignKey, SqlIdentifier column) =>
        Diagnostic.Error(Source, "unknown-local-column", $"Foreign key '{foreignKey.Member}' on '{foreignKey.Owner}' references unknown local column '{column}'.");

    /// <summary>
    /// A foreign key whose local and referenced column counts differ.
    /// </summary>
    public static Diagnostic ForeignKeyArityMismatch(MemberAddress foreignKey, int local, int referenced) =>
        Diagnostic.Error(Source, "foreign-key-arity-mismatch", $"Foreign key '{foreignKey.Member}' on '{foreignKey.Owner}' has {local} local column(s) but {referenced} referenced column(s).");

    /// <summary>
    /// A foreign key referencing a table the project does not declare, which must already exist in the database.
    /// </summary>
    public static Diagnostic UndeclaredForeignKeyTarget(MemberAddress foreignKey, ObjectAddress target) =>
        Diagnostic.Warning(Source, "undeclared-foreign-key-target", $"Foreign key '{foreignKey.Member}' on '{foreignKey.Owner}' references table '{target}', which this project does not declare; it must already exist in the database.");

    /// <summary>
    /// A foreign key referencing a column the target table does not declare.
    /// </summary>
    public static Diagnostic UnknownReferencedColumn(MemberAddress foreignKey, MemberAddress referenced) =>
        Diagnostic.Error(Source, "unknown-referenced-column", $"Foreign key '{foreignKey.Member}' on '{foreignKey.Owner}' references unknown column '{referenced.Member}' on '{referenced.Owner}'.");

    /// <summary>
    /// A foreign key referencing target columns that carry no uniqueness guarantee.
    /// </summary>
    public static Diagnostic ForeignKeyTargetNotUnique(MemberAddress foreignKey, ObjectAddress target, IEnumerable<SqlIdentifier> columns) =>
        Diagnostic.Error(Source, "foreign-key-target-not-unique", $"Foreign key '{foreignKey.Member}' on '{foreignKey.Owner}' references columns ({string.Join(", ", columns)}) on '{target}' that are not the primary key or a unique index.");
}
