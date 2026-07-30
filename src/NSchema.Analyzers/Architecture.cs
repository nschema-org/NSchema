namespace NSchema.Analyzers;

/// <summary>
/// The engine's slices and which of them may know about which. This table is the whole architecture — the analyzers
/// only read it, so adding an edge is a deliberate edit here rather than a change to a rule.
/// </summary>
public static class Architecture
{
    /// <summary>
    /// The composition root: the <c>NSchema</c> namespace itself, which hosts the application builder.
    /// </summary>
    public const string Root = "(root)";

    /// <summary>
    /// The shared vocabulary every slice may use: findings, the schema model they describe, and the collection helpers.
    /// Ambient outside itself, so no slice needs to declare it.
    /// </summary>
    public static readonly string[] Kernel = ["Diagnostics", "Extensions", "Model"];

    /// <summary>
    /// What each slice depends on. Dependency on the kernel is implicit.
    /// </summary>
    /// <remarks>
    /// A kernel slice lists its dependencies in full, since the kernel is ordered within itself.
    /// <c>Operations</c> is the shell that drives the slices, so it may use all of them and nothing may use it.
    /// <c>Plugins</c> is the one slice that may see the composition root, because registering onto the application builder is what a plugin does.
    /// The root itself is unconstrained: composing the slices is its job.
    /// </remarks>
    public static readonly Dictionary<string, string[]> Dependencies = new(StringComparer.Ordinal)
    {
        ["Diagnostics"] = [],
        ["Extensions"] = [],
        ["Model"] = ["Diagnostics", "Extensions"],
        ["Project"] = [],
        ["State"] = [],
        ["Deployment"] = [],
        ["Configuration"] = ["Project"],
        ["Plugins"] = ["Project", "Configuration", Root],
        ["Diff"] = ["Project", "State"],
        ["Plan"] = ["Project", "Diff"],
        ["Apply"] = ["Plan"],
        ["Operations"] = ["Project", "State", "Deployment", "Diff", "Plan", "Apply"],
    };

    /// <summary>
    /// Every named slice of the engine, the composition root included.
    /// </summary>
    public static readonly HashSet<string> Slices = new(Dependencies.Keys, StringComparer.Ordinal) { Root };

    /// <summary>
    /// The slices — everything that is not kernel and not the root.
    /// </summary>
    public static readonly HashSet<string> Features = new(Dependencies.Keys.Where(slice => !Kernel.Contains(slice)), StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="from"/> is allowed to reference <paramref name="to"/>.
    /// </summary>
    public static bool Allows(string from, string to) => from == to || from == Root || AllowedFor(from).Contains(to);

    /// <summary>
    /// Everything <paramref name="slice"/> may reference: its declared list, plus the kernel for anything outside it.
    /// </summary>
    public static string[] AllowedFor(string slice)
    {
        if (!Dependencies.TryGetValue(slice, out var declared))
        {
            return [];
        }

        return Kernel.Contains(slice) ? declared : [.. declared.Union(Kernel, StringComparer.Ordinal)];
    }
}
