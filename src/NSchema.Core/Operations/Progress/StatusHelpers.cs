using NSchema.Model;

namespace NSchema.Operations.Progress;

/// <summary>
/// Terse object counts for verbose output, shared by the operations that narrate what a schema contained.
/// </summary>
internal static class StatusHelpers
{
    /// <summary>
    /// A one-line census of a schema's top-level shape, e.g. "2 schemas, 7 tables".
    /// </summary>
    public static string Describe(Database database) =>
        $"{Count(database.Schemas.Count, "schema")}, {Count(database.Schemas.Sum(s => s.Tables.Count), "table")}";

    /// <summary>
    /// Formats a count with a singular/plural noun, e.g. "1 table" / "3 tables".
    /// </summary>
    public static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
