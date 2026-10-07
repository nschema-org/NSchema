namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// <c>[TABLES IN SCHEMA] schema</c> in a publication's <c>FOR</c> list.
/// </summary>
/// <param name="Schema">The schema whose tables are published.</param>
public sealed record PublishedSchemaTarget(Identifier Schema) : PublicationTarget
{
    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            foreach (var keyword in KindKeywords)
            {
                yield return keyword;
            }
            yield return Schema;
        }
    }
}
