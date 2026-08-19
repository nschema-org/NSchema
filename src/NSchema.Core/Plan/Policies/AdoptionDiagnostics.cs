using NSchema.Model;

namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="AdoptionPolicy"/>.
/// </summary>
internal static class AdoptionDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Adoption;

    // Enough to recognize what is being taken over without printing a whole database back at the user.
    private const int Listed = 5;

    /// <summary>
    /// Objects the apply takes over, which no statement in the plan mentions.
    /// </summary>
    public static Diagnostic ObjectsAdopted(IdentitySet adopted)
    {
        var names = adopted.DatabaseObjects.Select(o => o.Value)
            .Concat(adopted.SchemaObjects.Select(o => o.Value))
            .Order()
            .ToList();

        var subject = names.Count == 1 ? "1 existing object" : $"{names.Count} existing objects";
        var listed = names.Count > Listed
            ? $"{string.Join(", ", names.Take(Listed))}, and {names.Count - Listed} others"
            : string.Join(", ", names);

        return Diagnostic.Info(Source, "objects-adopted", $"Applying this plan will bring {subject:text} under management: {listed}.\n");
    }
}
