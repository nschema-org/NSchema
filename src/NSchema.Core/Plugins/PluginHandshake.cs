using System.Reflection;

namespace NSchema.Plugins;

/// <summary>
/// Validates that a loaded plugin assembly is binary-compatible with this engine, before any of its types are instantiated.
/// </summary>
/// <remarks>
/// The rule is deliberately narrow: the majors must match, and the engine must be at least as new as the
/// Core the plugin was built against. An older plugin on a newer engine is compatible by additive-API
/// discipline; a feature it never implemented surfaces as an <c>Unsupported</c> rendering at plan time.
/// </remarks>
public static class PluginHandshake
{
    private const string CoreAssemblyName = "NSchema.Core";

    private static readonly Version _hostVersion = typeof(PluginHandshake).Assembly.GetName().Version!;

    /// <summary>
    /// Validates <paramref name="pluginAssembly"/> against this engine.
    /// </summary>
    public static Result Validate(Assembly pluginAssembly)
    {
        var plugin = pluginAssembly.GetName().Name ?? pluginAssembly.ToString();
        var referencedCore = pluginAssembly.GetReferencedAssemblies()
            .FirstOrDefault(a => string.Equals(a.Name, CoreAssemblyName, StringComparison.OrdinalIgnoreCase));

        if (referencedCore is null)
        {
            return Result.From(HandshakeDiagnostics.DoesNotReferenceCore(plugin));
        }

        return referencedCore.Version is { } referenced ? Validate(plugin, referenced, _hostVersion) : Result.Success();
    }

    internal static Result Validate(string plugin, Version referenced, Version host)
    {
        if (referenced.Major != host.Major)
        {
            return Result.From(HandshakeDiagnostics.MajorMismatch(plugin, referenced, host));
        }
        if (referenced.Minor > host.Minor)
        {
            return Result.From(HandshakeDiagnostics.EngineOlderThanPlugin(plugin, referenced, host));
        }
        return Result.Success();
    }
}
