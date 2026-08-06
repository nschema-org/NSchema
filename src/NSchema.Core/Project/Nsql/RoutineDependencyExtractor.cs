using System.Text.RegularExpressions;
using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Services;

namespace NSchema.Project.Nsql;

/// <summary>
/// Extracts the objects a routine's (opaque) definition references: what its queries read, plus the
/// routines it calls.
/// </summary>
/// <remarks>
/// Like <see cref="ViewDependencyExtractor"/>, over-collecting is free — a reference only forms an
/// ordering edge when it names an object in the same plan — so the call scan errs wide: every
/// <c>name(</c> is collected, and aggregates, casts, and builtins match no planned object.
/// </remarks>
internal static partial class RoutineDependencyExtractor
{
    /// <summary>
    /// Extracts the dependencies of a routine from its definition.
    /// </summary>
    /// <param name="definition">The routine's verbatim definition (everything after the argument list).</param>
    /// <param name="defaultSchema">The schema an unqualified reference is resolved against (the routine's own schema).</param>
    /// <param name="kind">The routine kind, which decides what a reference looks like in the definition.</param>
    public static List<ObjectAddress> Extract(SqlText definition, SqlIdentifier defaultSchema, RoutineKind kind = RoutineKind.Function)
    {
        var result = ViewDependencyExtractor.Extract(definition, defaultSchema);
        var seen = result.ToHashSet();

        foreach (var address in ExpressionDependencyScanner.CallSites(definition.Value, defaultSchema))
        {
            if (seen.Add(address))
            {
                result.Add(address);
            }
        }

        // An aggregate has no body: its definition names the functions it is assembled from as option
        // values (SFUNC = _group_concat), which are not call sites, so those are collected too.
        if (kind == RoutineKind.Aggregate)
        {
            foreach (Match match in OptionValue().Matches(definition.Value))
            {
                var schema = match.Groups["schema"];
                var address = schema.Success
                    ? new ObjectAddress(schema.Value, match.Groups["name"].Value)
                    : new ObjectAddress(defaultSchema, match.Groups["name"].Value);
                if (seen.Add(address))
                {
                    result.Add(address);
                }
            }
        }

        return result;
    }

    // An option value: = name or = schema.name. Non-object values (types, literals) over-collect harmlessly.
    [GeneratedRegex(@"=\s*(?:(?<schema>[A-Za-z_][\w$]*)\s*\.\s*)?(?<name>[A-Za-z_][\w$]*)")]
    private static partial Regex OptionValue();
}
