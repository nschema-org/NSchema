using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace NSchema.Tests.Architecture;

/// <summary>
/// The engine's architecture and the parts its rules are written against. Every part is named here once, so two
/// rules about the same part cannot disagree about what it covers, and a rule never names a type.
/// </summary>
public static class CoreArchitecture
{
    /// <summary>
    /// The composition root: the namespace <c>NSchema</c> itself, which hosts the application builder.
    /// </summary>
    public const string Root = "(root)";

    /// <summary>
    /// The loaded engine assembly. Loading is slow, so every rule shares this one.
    /// </summary>
    public static readonly ArchUnitNET.Domain.Architecture Assembly =
        new ArchLoader().LoadAssemblies(typeof(NSchemaApplication).Assembly).Build();

    /// <summary>
    /// The shared vocabulary every slice may use: findings, the schema model they describe, and the collection
    /// helpers. The first two are global usings in the engine's project file, which is the same statement in
    /// MSBuild form — they are available everywhere without ceremony because everything may use them.
    /// </summary>
    public static readonly string[] Kernel = ["Diagnostics", "Extensions", "Model"];

    /// <summary>
    /// The feature slices, each owning one stage of the pipeline or one piece of infrastructure.
    /// </summary>
    public static readonly string[] Features =
        ["Project", "State", "Deployment", "Configuration", "Plugins", "Diff", "Plan", "Apply", "Operations"];

    /// <summary>
    /// Every named part of the engine, the composition root included.
    /// </summary>
    public static string[] Parts => [.. Kernel, .. Features, Root];

    /// <summary>
    /// The types in the named parts, and everything nested beneath them.
    /// </summary>
    public static IObjectProvider<IType> In(params string[] parts) =>
        Types().That().ResideInNamespaceMatching(Pattern(parts)).As(Describe(parts));

    /// <summary>
    /// Each slice's domain: its model and the services over it. Pure — it answers questions about the schema and
    /// never reaches for infrastructure.
    /// </summary>
    public static readonly IObjectProvider<IType> DomainLayers =
        Types().That().ResideInNamespaceMatching(@"^NSchema\.\w+\.Domain($|\.)").As("the slices' domain layers");

    /// <summary>
    /// Each slice's contract: the services other slices call it through, which sit directly in the slice namespace
    /// (<c>IDatabaseStateManager</c>, <c>IProjectProvider</c>, and the like).
    /// </summary>
    public static readonly IObjectProvider<IType> ApplicationServices =
        Types().That().ResideInNamespaceMatching(SliceRootPattern(Features)).As("the slices' application services");

    /// <summary>
    /// The seams a provider package implements downstream — dialects, introspectors, state stores.
    /// </summary>
    /// <remarks>
    /// <c>NSchema.Configuration.Plugins</c> is excluded: it configures which plugins a project declares, rather than
    /// being something a plugin implements.
    /// </remarks>
    public static readonly IObjectProvider<IType> ProviderSeams =
        Types().That().ResideInNamespaceMatching(@"^NSchema\.(?!Configuration\.)\w+(\.\w+)*\.Plugins($|\.)").As("the provider seams");

    /// <summary>
    /// The violations of <paramref name="rule"/>, empty when it holds.
    /// </summary>
    public static IEnumerable<string> Violations(IArchRule rule) =>
        rule.Evaluate(Assembly).Where(result => !result.Passed).Select(result => result.Description);

    /// <summary>
    /// A regex matching each part's namespace and everything beneath it.
    /// </summary>
    private static string Pattern(params string[] parts) =>
        "^(" + string.Join("|", parts.Select(p => p == Root ? "NSchema$" : $@"NSchema\.{p}($|\.)")) + ")";

    /// <summary>
    /// A regex matching each part's namespace exactly — the slice root, not its sub-namespaces.
    /// </summary>
    private static string SliceRootPattern(params string[] parts) =>
        "^(" + string.Join("|", parts.Select(p => $@"NSchema\.{p}")) + ")$";

    private static string Describe(string[] parts) =>
        parts.Length == 0 ? "nothing" : string.Join(", ", parts);
}
