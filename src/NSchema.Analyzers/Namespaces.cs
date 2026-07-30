namespace NSchema.Analyzers;

/// <summary>
/// Reads a namespace and determines which slice of the architecture it belongs to, and what shape of thing it holds.
/// Pure string work — everything the rules know about a type, they learn from where it lives.
/// </summary>
public static class Namespaces
{
    private const string Root = "NSchema";

    /// <summary>
    /// The slice <paramref name="ns"/> belongs to (<c>NSchema.Plan.Domain.Services</c> is <c>Plan</c>),
    /// or <see langword="null"/> when it is outside the engine entirely.
    /// </summary>
    public static string? SliceOf(string ns)
    {
        var segments = ns.Split('.');

        if (segments[0] != Root)
        {
            return null;
        }

        return segments.Length == 1 ? Architecture.Root : segments[1];
    }

    /// <summary>
    /// Whether <paramref name="ns"/> is a slice's contract — the services other slices call it through, which sit
    /// directly in the slice namespace (<c>NSchema.Project</c>, <c>NSchema.State</c>).
    /// </summary>
    public static bool IsApplication(string ns)
    {
        var segments = ns.Split('.');
        return segments.Length == 2 && segments[0] == Root && Architecture.Features.Contains(segments[1]);
    }

    /// <summary>
    /// Whether <paramref name="ns"/> is a slice's domain — its model and the services over it
    /// (<c>NSchema.Diff.Domain</c> and anything beneath).
    /// </summary>
    public static bool IsDomain(string ns)
    {
        var segments = ns.Split('.');
        return segments.Length >= 3 && segments[0] == Root && segments[2] == "Domain";
    }

    /// <summary>
    /// Whether <paramref name="ns"/> is a seam a provider package implements downstream — dialects, introspectors,
    /// state stores (<c>NSchema.Plan.Plugins</c>, <c>NSchema.State.Locks.Plugins</c>).
    /// </summary>
    /// <remarks>
    /// <c>NSchema.Configuration.Plugins</c> is excluded: it configures which plugins a project declares, rather than
    /// being something a plugin implements. <c>NSchema.Plugins</c> is the contract slice itself, not a seam.
    /// </remarks>
    public static bool IsProviderSeam(string ns)
    {
        var segments = ns.Split('.');

        if (segments.Length < 3 || segments[0] != Root || segments[1] == "Configuration")
        {
            return false;
        }

        // "Plugins" from the third segment on — the slice's own seam folder, never the top-level Plugins slice.
        return segments.Skip(2).Contains("Plugins");
    }
}
