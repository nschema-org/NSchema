namespace NSchema.Model;

/// <summary>
/// The kind of an object the database owns directly.
/// </summary>
public enum DatabaseObjectKind
{
    /// <summary>
    /// A schema, which holds objects of its own.
    /// </summary>
    Schema,

    /// <summary>
    /// An extension, which holds nothing.
    /// </summary>
    Extension
}

/// <summary>
/// Rendering for <see cref="DatabaseObjectKind"/>.
/// </summary>
internal static class DatabaseObjectKindExtensions
{
    /// <summary>
    /// The kind as display prose, for diagnostics.
    /// </summary>
    public static string Display(this DatabaseObjectKind kind) => kind switch
    {
        DatabaseObjectKind.Schema => "schema",
        DatabaseObjectKind.Extension => "extension",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
