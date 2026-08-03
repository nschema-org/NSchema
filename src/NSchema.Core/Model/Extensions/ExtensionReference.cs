namespace NSchema.Model.Extensions;

/// <summary>
/// A reference to an extension. Extensions are database-global, so a name is the whole address.
/// </summary>
/// <param name="Name">The extension's name.</param>
public sealed record ExtensionReference(SqlIdentifier Name)
{
    /// <summary>
    /// The reference as written.
    /// </summary>
    public override string ToString() => Name.Value;
}
