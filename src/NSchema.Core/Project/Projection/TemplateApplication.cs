using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Projection;

/// <summary>
/// An <c>APPLY TEMPLATE</c> statement paired with the file that declared it, so instantiation findings
/// attribute to their source.
/// </summary>
internal readonly record struct TemplateApplication(ApplyTemplateStatement Statement, string? File);
