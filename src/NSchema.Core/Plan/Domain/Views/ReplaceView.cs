using NSchema.Model;
using NSchema.Model.Views;

namespace NSchema.Plan.Domain.Views;

/// <summary>
/// Represents the in-place body replacement of an existing view.
/// </summary>
/// <param name="SchemaName">The name of the schema the view belongs to.</param>
/// <param name="View">The definition replacing the existing body.</param>
public sealed record ReplaceView(SqlIdentifier SchemaName, View View) : MigrationAction;
