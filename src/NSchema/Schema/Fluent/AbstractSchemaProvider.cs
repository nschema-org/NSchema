using NSchema.Migration;

namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides an abstract base class for defining a desired database schema using a fluent interface.
/// </summary>
public abstract class AbstractSchemaProvider : IDesiredSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];

    /// <summary>
    /// Adds a new schema with the specified name to the desired database schema and returns a builder for configuring it.
    /// </summary>
    /// <param name="name">The name of the schema to define.</param>
    /// <returns>A <see cref="SchemaBuilder"/> instance that can be used to configure the schema.</returns>
    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds a new schema with the specified name to the desired database schema and returns a builder for configuring it.
    /// </summary>
    /// <param name="name">The name of the schema to define.</param>
    /// <param name="configure">A delegate that can be used to configure the schema.</param>
    /// <returns>The current schema provider so that calls can be chained.</returns>
    public AbstractSchemaProvider Schema(string name, Action<SchemaBuilder> configure)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        configure.Invoke(builder);
        return this;
    }

    /// <inheritdoc/>
    public Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default)
    {
        var schemas = _schemas.Where(s => !s.IsDropped).Select(s => s.Build()).ToList();
        var droppedSchemas = _schemas.Where(s => s.IsDropped).Select(s => s.Name).ToList();
        return Task.FromResult(new DatabaseSchema(schemas, droppedSchemas));
    }
}
