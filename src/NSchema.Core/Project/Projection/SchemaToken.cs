using NSchema.Model;

namespace NSchema.Project.Projection;

/// <summary>
/// The <c>{schema}</c> substitution token a template script may carry in its SQL body, and the substitution
/// that instantiates it for an applied schema. The single home of the token's spelling.
/// </summary>
internal static class SchemaToken
{
    public const string Token = "{schema}";

    /// <summary>
    /// The schema name a template body binds to when projected outside a real application — validation and
    /// include resolution; re-pointed at the applied schema when an instance merges.
    /// </summary>
    public static readonly SqlIdentifier TargetSchemaPlaceholder = new("<template>");

    /// <summary>
    /// Instantiates the token in raw text (a SQL body).
    /// </summary>
    public static SqlText Instantiate(SqlText text, SqlIdentifier schema) =>
        new(text.Value.Replace(Token, schema.Value, StringComparison.Ordinal));
}
