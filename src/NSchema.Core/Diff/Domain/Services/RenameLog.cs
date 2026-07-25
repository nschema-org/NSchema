using NSchema.Model;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// The operations the aligner applied while renaming the old schema objects.
/// </summary>
internal sealed class RenameLog
{
    private readonly IReadOnlyDictionary<SchemaAddress, SqlIdentifier> _schemas;
    private readonly IReadOnlyDictionary<ObjectAddress, SqlIdentifier> _objects;
    private readonly IReadOnlyDictionary<MemberAddress, SqlIdentifier> _columns;

    internal RenameLog(
        IReadOnlyDictionary<SchemaAddress, SqlIdentifier> schemas,
        IReadOnlyDictionary<ObjectAddress, SqlIdentifier> objects,
        IReadOnlyDictionary<MemberAddress, SqlIdentifier> columns
    )
    {
        _schemas = schemas;
        _objects = objects;
        _columns = columns;
    }

    /// <summary>
    /// The empty log — nothing was renamed.
    /// </summary>
    public static RenameLog Empty { get; } = new(
        new Dictionary<SchemaAddress, SqlIdentifier>(),
        new Dictionary<ObjectAddress, SqlIdentifier>(),
        new Dictionary<MemberAddress, SqlIdentifier>()
    );

    /// <summary>
    /// The previous name of the schema now named <paramref name="declared"/>, or <see langword="null"/> when
    /// it was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(SchemaAddress declared) => _schemas.GetValueOrDefault(declared);

    /// <summary>
    /// The previous name of the object now at <paramref name="declared"/>, or <see langword="null"/> when it
    /// was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(ObjectAddress declared) => _objects.GetValueOrDefault(declared);

    /// <summary>
    /// The previous name of the column now at <paramref name="declared"/>, or <see langword="null"/> when it
    /// was not renamed.
    /// </summary>
    public SqlIdentifier? RenamedFrom(MemberAddress declared) => _columns.GetValueOrDefault(declared);
}
