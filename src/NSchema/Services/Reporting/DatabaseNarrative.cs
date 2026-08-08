using NSchema.Model;
using NSchema.State.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// The wording the text faces share when summarizing a schema: the per-kind object counts the quiet faces render in
/// place of the full rendering.
/// </summary>
internal static class DatabaseNarrative
{
    /// <summary>
    /// The one-line schema summary the quiet face renders in place of the full rendering.
    /// </summary>
    public static string Summary(Database database) => $"Database: {Counts(database)}.";

    /// <summary>
    /// The one-line state summary the quiet face renders: the recorded schema's counts, how much of it is managed,
    /// and the size of the script ledger.
    /// </summary>
    public static string Summary(DatabaseState state)
    {
        var managed = state.Managed.DatabaseObjects.Count + state.Managed.SchemaObjects.Count;
        return $"State: {Counts(state.Database)}. {managed} managed object{(managed == 1 ? string.Empty : "s")}, "
            + $"{state.Scripts.Count} recorded script execution{(state.Scripts.Count == 1 ? string.Empty : "s")}.";
    }

    /// <summary>
    /// The per-kind object counts, listing only the kinds that are present ("2 schemas, 3 tables, 1 view").
    /// </summary>
    public static string Counts(Database database)
    {
        var schemas = database.Schemas;
        var parts = new List<string>();

        Add(parts, schemas.Count, "schema");
        Add(parts, schemas.Sum(s => s.Tables.Count), "table");
        Add(parts, schemas.Sum(s => s.Views.Count), "view");
        Add(parts, schemas.Sum(s => s.Sequences.Count), "sequence");
        Add(parts, schemas.Sum(s => s.Routines.Count), "routine");
        Add(parts, schemas.Sum(s => s.Domains.Count), "domain");
        Add(parts, schemas.Sum(s => s.Enums.Count), "enum");
        Add(parts, schemas.Sum(s => s.CompositeTypes.Count), "composite type");
        Add(parts, database.Extensions.Count, "extension");

        return parts.Count == 0 ? "no objects" : string.Join(", ", parts);
    }

    private static void Add(List<string> parts, int count, string kind)
    {
        if (count > 0)
        {
            parts.Add($"{count} {kind}{(count == 1 ? string.Empty : "s")}");
        }
    }
}
