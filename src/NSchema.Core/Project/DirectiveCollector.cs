using NSchema.Model;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Projection;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project;

/// <summary>
/// Collects directive statements across a project's documents and builds the <see cref="ProjectDirectives"/>.
/// </summary>
internal sealed class DirectiveCollector
{
    private readonly ProjectedScripts _scripts = new();
    private readonly List<SchemaRenameDirective> _schemaRenames = [];
    private readonly List<ObjectRenameDirective> _renames = [];
    private readonly List<MemberRenameDirective> _columnRenames = [];

    /// <summary>
    /// Consumes a directive statement, returning <see langword="false"/> when the statement is not one.
    /// </summary>
    /// <param name="statement">The statement to consume.</param>
    /// <param name="context">The schema the directive binds to, when inside a template.</param>
    public bool TryAdd(NsqlStatement statement, SqlIdentifier? context = null)
    {
        switch (statement)
        {
            case Syn.Scripts.ScriptStatement s:
                DocumentProjector.ProjectScript(s, context, _scripts);
                return true;
            case Syn.Schemas.RenameSchemaStatement s:
                _schemaRenames.Add(new SchemaRenameDirective(new SchemaAddress(Name(s.From)), new SchemaAddress(Name(s.To))));
                return true;
            case RenameObjectStatement s:
                _renames.Add(new ObjectRenameDirective(Reference(s.From, context) with { Kind = s.Kind }, Name(s.To)));
                return true;
            case Syn.Tables.RenameColumnStatement s:
                _columnRenames.Add(new MemberRenameDirective(
                    new MemberAddress(Bind(s.From.Schema, context), Name(s.From.Table), Name(s.From.Member)),
                    Name(s.To)));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Merges another set of directives into this collector.
    /// </summary>
    public void Absorb(ProjectDirectives other)
    {
        _scripts.Change.AddRange(other.ChangeScripts);
        _scripts.Deployment.AddRange(other.DeploymentScripts);
        _schemaRenames.AddRange(other.SchemaRenames);
        _renames.AddRange(other.ObjectRenames);
        _columnRenames.AddRange(other.MemberRenames);
    }

    public ProjectDirectives Build() => new(
        _schemaRenames,
        _renames,
        _columnRenames,
        _scripts.Change,
        _scripts.Deployment
    );

    private static SqlIdentifier Name(Identifier identifier) => new(identifier.Value);

    /// <summary>
    /// Translates a directive's qualified name into an address, binding an unqualified name (only legal inside
    /// a template body) to the applied schema.
    /// </summary>
    private static ObjectAddress Reference(QualifiedName name, SqlIdentifier? context) =>
        new(Bind(name.Schema, context), Name(name.Name));

    /// <summary>
    /// Resolves a directive's schema: the written one, or the applied schema inside a template body.
    /// </summary>
    private static SqlIdentifier Bind(Identifier? schema, SqlIdentifier? context) => schema != null
        ? Name(schema)
        : context ?? throw new InvalidOperationException("Directive reference has no schema part outside a template.");
}
