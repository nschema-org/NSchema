using NSchema.Model.Extensions;

namespace NSchema.Plan.Domain.Extensions;

/// <summary>
/// Represents the creation of a database extension.
/// </summary>
/// <param name="Extension">The definition of the extension to create.</param>
public sealed record CreateExtension(Extension Extension) : MigrationAction;
