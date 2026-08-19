namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// A single field of a composite type: a name and a type.
/// </summary>
/// <param name="Name">The field name.</param>
/// <param name="Type">The field type as written.</param>
public sealed record CompositeFieldDefinition(Identifier Name, TypeName Type) : NsqlNode
{
    internal override IEnumerable<NsqlChild> Children => [Name, Type];
}
