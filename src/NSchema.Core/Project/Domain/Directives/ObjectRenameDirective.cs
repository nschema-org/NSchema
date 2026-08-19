using NSchema.Model;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// A schema-level object rename directive.
/// </summary>
/// <remarks>Renames never move an object across containers, so the target is a bare name.</remarks>
/// <param name="From">The identity of the object being renamed).</param>
/// <param name="To">The declared name the object is renamed to.</param>
public sealed record ObjectRenameDirective(ObjectAddress From, SqlIdentifier To)
{
    /// <summary>
    /// The object's address after the rename — the same container, the new name.
    /// </summary>
    public ObjectAddress ToAddress => From with { Name = To };
}
