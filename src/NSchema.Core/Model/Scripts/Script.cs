using System.Text.Json.Serialization;
namespace NSchema.Model.Scripts;

/// <summary>
/// A raw-SQL script and the run semantics common to every script kind.
/// </summary>
/// <param name="Name">The name that identifies the script.</param>
/// <param name="Sql">The raw SQL to run.</param>
/// <param name="ScopeSchema">The schema the run is scoped to, or <see langword="null"/> when the script is global.</param>
public abstract record Script(SqlIdentifier Name, SqlText Sql, SqlIdentifier? ScopeSchema)
{
    /// <summary>
    /// The canonical hash of the script body.
    /// </summary>
    public ScriptHash Hash => ScriptHash.Compute(Sql);

    /// <summary>
    /// What identifies the script: its scope schema and its name.
    /// </summary>
    [JsonIgnore]
    public ScriptReference Reference => new(ScopeSchema, Name);

    /// <summary>
    /// When true, the script runs outside the migration's transaction.
    /// </summary>
    public bool RunOutsideTransaction { get; init; }

    /// <summary>
    /// The event as written in DDL source.
    /// </summary>
    public abstract string Description { get; }
}
