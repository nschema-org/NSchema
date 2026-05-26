namespace NSchema.Migration;

/// <summary>
/// Marker interface identifying the <see cref="ISchemaProvider"/> that supplies the current (live) database schema.
/// </summary>
public interface ICurrentSchemaProvider : ISchemaProvider;
