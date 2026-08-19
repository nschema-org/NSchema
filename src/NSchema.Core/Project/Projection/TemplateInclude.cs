using NSchema.Model;

namespace NSchema.Project.Projection;

/// <summary>
/// An <c>INCLUDE name</c> member inside a table body, targeting the table it was written in by name.
/// </summary>
/// <param name="Table">The address of the including table (its schema is the placeholder for a table declared inside a schema template).</param>
/// <param name="TemplateName">The name of the table template to include.</param>
/// <param name="ColumnPosition">The index among the table's declared columns where the include appeared.</param>
internal sealed record TemplateInclude(ObjectAddress Table, SqlIdentifier TemplateName, int ColumnPosition);
