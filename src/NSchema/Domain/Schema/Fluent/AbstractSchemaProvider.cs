using NSchema.Desired;

namespace NSchema.Domain.Schema.Fluent;

public abstract class AbstractSchemaProvider : IDesiredSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<Script> _preScripts = [];
    private readonly List<Script> _postScripts = [];

    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    public void PreDeploymentScript(string name, string sql)
    {
        _preScripts.Add(new Script(name, sql));
    }

    public void PostDeploymentScript(string name, string sql)
    {
        _postScripts.Add(new Script(name, sql));
    }

    public Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default)
    {
        var schemas = _schemas.Select(s => s.Build()).ToList();
        var schema = new DatabaseSchema(schemas, _preScripts, _postScripts);
        return Task.FromResult(schema);
    }
}
