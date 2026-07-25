using NSchema.Model;

namespace NSchema.Plan.Domain.Views;

/// <summary>
/// Represents renaming an existing view.
/// </summary>
/// <param name="View">The address of the view.</param>
/// <param name="NewName">The new name of the view.</param>
/// <param name="IsMaterialized">Whether the view being renamed is a materialized view.</param>
public sealed record RenameView(ObjectAddress View, SqlIdentifier NewName, bool IsMaterialized = false) : MigrationAction;
