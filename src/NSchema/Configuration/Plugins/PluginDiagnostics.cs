using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The diagnostics the CLI mints while resolving, restoring and loading plugins.
/// </summary>
/// <remarks>
/// The source is shared with the engine's own plugin findings (the handshake, the lockfile) so that grouping or
/// configuring by source covers the whole subsystem, whichever side of the plugin boundary reported it. The codes
/// stay distinct, because a code names one finding.
/// </remarks>
internal static class PluginDiagnostics
{
    internal static readonly DiagnosticSource Source = "plugins";

    /// <summary>
    /// A package that restored and loaded, but exports no plugin type.
    /// </summary>
    public static Diagnostic NoPluginInPackage(PackageId package) =>
        Diagnostic.Error(Source, "no-plugin-in-package", $"The package '{package}' does not contain an NSchema plugin.");

    /// <summary>
    /// A plugin whose types will not bind against the host's NSchema.Core.
    /// </summary>
    public static Diagnostic IncompatiblePlugin(PackageId package, SemanticVersion? version, string reason) =>
        Diagnostic.Error(Source, "incompatible-plugin",
            $"The plugin {Describe(package, version):text} was built for a different version of NSchema and cannot be loaded.\n"
            + $"Update it with 'nschema plugin update', or set a compatible version in its PLUGIN statement. ({reason:text})");

    /// <summary>
    /// A plugin that failed to load for any other reason.
    /// </summary>
    public static Diagnostic LoadFailed(PackageId package, SemanticVersion? version, string reason) =>
        Diagnostic.Error(Source, "plugin-load-failed", $"The plugin {Describe(package, version):text} could not be loaded: {reason}");

    // A package names itself with its version; a path-loaded plugin has none, and saying so beats printing a blank.
    private static string Describe(PackageId package, SemanticVersion? version) =>
        version is null ? $"'{package}' (loaded from a path)" : $"'{package}' {version}";

    /// <summary>
    /// A package the feeds offer no version of that this host can load.
    /// </summary>
    public static Diagnostic NoCompatibleVersion(PackageId package, int hostMajor) =>
        Diagnostic.Error(Source, "no-compatible-version", $"No version of '{package}' is available for NSchema {hostMajor}.x.");

    /// <summary>
    /// A declared version range that no available version satisfies.
    /// </summary>
    public static Diagnostic NoMatchingVersion(PackageId package, VersionRange range, int hostMajor) =>
        Diagnostic.Error(Source, "no-matching-version",
            $"No version of '{package}' satisfying '{range}' is available for NSchema {hostMajor}.x.");

    /// <summary>
    /// A plugin that is not cached, on a run that may not restore one.
    /// </summary>
    public static Diagnostic NotRestored(PackageId package, SemanticVersion version) =>
        Diagnostic.Error(Source, "plugin-not-restored",
            $"Plugin '{package}' {version} is not restored, and --no-init was specified. Run 'nschema init' (or drop --no-init) to restore it first.");

    /// <summary>
    /// A restore that gave up waiting on the concurrent run holding the cache lock.
    /// </summary>
    public static Diagnostic RestoreTimeout(PackageId package, SemanticVersion version) =>
        Diagnostic.Error(Source, "restore-timeout",
            $"Timed out waiting for another process to finish restoring plugin '{package}' {version}.");

    /// <summary>
    /// A package that restored, but carries no assembly under its own name to load.
    /// </summary>
    public static Diagnostic AssemblyMissing(PackageId package, SemanticVersion version) =>
        Diagnostic.Error(Source, "plugin-assembly-missing",
            $"Restored package '{package}' {version} but its assembly '{package}.dll' was not found — is the package an NSchema plugin?");

    /// <summary>
    /// A label on a <c>DATABASE</c>/<c>STATE</c> statement that no <c>PLUGIN</c> statement declares.
    /// </summary>
    public static Diagnostic NotDeclared(PluginLabel label) =>
        Diagnostic.Error(Source, "plugin-not-declared",
            $"'{label}' does not reference a declared plugin. Add: PLUGIN {label} ( source = '...', version = '...' );");

    /// <summary>
    /// A declared plugin path whose file name is not a usable assembly name.
    /// </summary>
    public static Diagnostic UnusablePluginPath(PluginLabel label, string path) =>
        Diagnostic.Error(Source, "unusable-plugin-path",
            $"Plugin '{label}' points at {path:text}, which is invalid. The path must name the plugin assembly directly.");

    /// <summary>
    /// A declared plugin path with nothing at it.
    /// </summary>
    public static Diagnostic PluginPathNotFound(PluginLabel label, string path) =>
        Diagnostic.Error(Source, "plugin-path-not-found",
            $"Plugin '{label}' points at {path:text}, which does not exist. Paths are resolved against the project root.");

    /// <summary>
    /// A plugin assembly with no dependency manifest beside it.
    /// </summary>
    /// <remarks>
    /// Worth its own diagnostic because the runtime's own account of this is unhelpful — a dependency resolution
    /// failure naming a component that was never there. The cause is nearly always a plugin built without its
    /// dependency closure, which leaves the assembly on disk but none of what it needs beside it.
    /// </remarks>
    public static Diagnostic PluginPathNotSelfContained(PluginLabel label, string path) =>
        Diagnostic.Error(Source, "plugin-path-not-self-contained",
            $"Plugin '{label}' at {path:text} has no .deps.json beside it, so its dependencies cannot be resolved. Build the plugin with <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> so its full dependency closure is written alongside it.");

    /// <summary>
    /// A declared plugin whose version range the lockfile does not pin.
    /// </summary>
    /// <remarks>
    /// Shares the engine's code: the same finding, reported by whichever side resolved the lock, and this side can
    /// name the command that fixes it.
    /// </remarks>
    public static Diagnostic NotLocked(PackageId package, VersionRange range) =>
        Diagnostic.Error(Source, "plugin-not-locked",
            $"Plugin '{package}' is declared as '{range}' but is not locked. Run 'nschema init' to resolve and lock it.");

    /// <summary>
    /// A label the user asked about that this project configures no plugin for.
    /// </summary>
    public static Diagnostic NotConfigured(string? label, string configured) =>
        Diagnostic.Error(Source, "plugin-not-configured",
            $"No plugin labelled '{label}' is configured for this project (configured: {configured}).");

    /// <summary>
    /// A package that supplies no plugin for the capability the statement it is referenced from needs.
    /// </summary>
    public static Diagnostic MissingCapability(PackageId package) =>
        Diagnostic.Error(Source, "missing-plugin-capability",
            $"The package '{package}' does not provide the expected plugin capability.");

    /// <summary>
    /// The .NET SDK, which plugin resolution shells out to, is not on the PATH.
    /// </summary>
    public static Diagnostic DotnetNotFound() =>
        Diagnostic.Error(Source, "dotnet-not-found",
            "NSchema needs the .NET SDK ('dotnet') on your PATH to resolve plugins, but it could not be found.");

    /// <summary>
    /// A <c>dotnet</c> invocation that plugin resolution depends on exited non-zero.
    /// </summary>
    public static Diagnostic DotnetFailed(int exitCode, string output) =>
        Diagnostic.Error(Source, "dotnet-failed",
            $"An NSchema plugin operation failed (dotnet exit code {exitCode}):{Environment.NewLine:text}{output:text}");

    /// <summary>
    /// A plugin loaded from a path rather than a pinned package. Raised on every run that loads one.
    /// </summary>
    /// <remarks>
    /// Information rather than a warning: it reports how the run was configured, not a fault in the project. Someone
    /// who wrote a path meant to, and until findings can be configured a warning they cannot turn off would be noise
    /// on every command for as long as they were working. It still has to be said, because whoever reads a log needs
    /// to tell a run against a build from a run against a release — revisit the severity once it can be silenced.
    /// </remarks>
    public static Diagnostic PluginLoadedFromPath(PluginLabel label, string path) => Diagnostic.Info(Source, "plugin-from-path",
        $"Plugin '{label}' is loaded from {path:text} rather than a pinned package, so this project is not reproducible from its lockfile.");
}
