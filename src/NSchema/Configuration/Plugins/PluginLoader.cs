using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using NSchema.Plugins;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Resolves NSchema plugins (providers and backends) from NuGet packages at runtime.
/// </summary>
internal sealed class PluginLoader(string? cacheRoot = null)
{
    private const string SynthAssemblyName = "nschema-plugin-host";

    // PluginCache is the single source of truth for the on-disk layout; the loader populates what it describes.
    private readonly PluginCache _cache = new(cacheRoot);

    /// <summary>
    /// Loads the plugin(s) the given package contains at the given version, restoring the package on first use.
    /// </summary>
    public IReadOnlyList<INSchemaPlugin> Load(string packageId, string version, bool allowRestore = true)
    {
        var publishDir = EnsurePublished(packageId, version, allowRestore);

        // The synthesized host project is the resolver root: its .deps.json describes the whole closure, including
        // the plugin package itself, which we then load by name through the isolated context.
        var context = new PluginLoadContext(Path.Combine(publishDir, SynthAssemblyName + ".dll"));
        var assembly = context.LoadFromAssemblyName(new AssemblyName(packageId));

        VerifyCoreCompatibility(packageId, assembly);

        var plugins = assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(INSchemaPlugin).IsAssignableFrom(t))
            .Select(t => (INSchemaPlugin)Activator.CreateInstance(t)!)
            .ToList();

        if (plugins.Count == 0)
        {
            throw new InvalidOperationException($"The package '{packageId}' does not contain an NSchema plugin.");
        }

        return plugins;
    }

    /// <summary>
    /// Resolves the latest available version of <paramref name="packageId"/> that shares this host's NSchema.Core
    /// major version, by floating-restoring it and reading the resolved version back from the lock file. Used when
    /// scaffolding, where no version is pinned yet.
    /// </summary>
    public string ResolveLatestVersion(string packageId)
    {
        var hostMajor = typeof(INSchemaPlugin).Assembly.GetName().Version?.Major
            ?? throw new InvalidOperationException("Could not determine the host NSchema.Core major version.");

        var projectDir = _cache.ResolveDirectory(packageId);
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, SynthAssemblyName + ".csproj");

        // Float within the host major (prereleases included) so the pin always matches a plugin this CLI can load.
        File.WriteAllText(projectPath, SynthProject(packageId, $"{hostMajor}.*-*"));

        // --force re-evaluates the floating range every time, so a newly published version is picked up.
        RunDotnet("restore", projectPath, "--force");

        return ReadResolvedVersion(Path.Combine(projectDir, "obj", "project.assets.json"), packageId);
    }

    private static string ReadResolvedVersion(string assetsPath, string packageId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
        {
            // Library keys are "Id/Version"; match the package id case-insensitively.
            var separator = library.Name.IndexOf('/');
            if (separator > 0 && library.Name.AsSpan(0, separator).Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return library.Name[(separator + 1)..];
            }
        }

        throw new InvalidOperationException($"Could not resolve a version for plugin package '{packageId}'.");
    }

    private string EnsurePublished(string packageId, string version, bool allowRestore)
    {
        var publishDir = _cache.PublishDirectory(packageId, version);
        var pluginAssembly = _cache.PluginAssembly(packageId, version);

        // Restoring a plugin is expensive, so a published closure is reused across runs.
        if (File.Exists(pluginAssembly))
        {
            return publishDir;
        }

        // --no-init: stay offline and require the plugin to be cached already.
        if (!allowRestore)
        {
            throw new InvalidOperationException(
                $"Plugin '{packageId}' {version} is not restored, and --no-init was specified. Run 'nschema init' (or drop --no-init) to restore it first.");
        }

        var projectDir = _cache.ProjectDirectory(packageId, version);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, SynthAssemblyName + ".csproj"), SynthProject(packageId, version));

        RunDotnet("publish", Path.Combine(projectDir, SynthAssemblyName + ".csproj"), "-c", "Release", "-o", publishDir);

        if (!File.Exists(pluginAssembly))
        {
            throw new InvalidOperationException(
                $"Restored package '{packageId}' {version} but its assembly '{packageId}.dll' was not found — is the package an NSchema plugin?");
        }

        return publishDir;
    }

    // EnableDynamicLoading emits the .deps.json and copies the full dependency closure that the load context needs.
    private static string SynthProject(string packageId, string version) =>
        $"""
         <Project Sdk="Microsoft.NET.Sdk">
           <PropertyGroup>
             <TargetFramework>net10.0</TargetFramework>
             <EnableDynamicLoading>true</EnableDynamicLoading>
             <IsPackable>false</IsPackable>
           </PropertyGroup>
           <ItemGroup>
             <PackageReference Include="{packageId}" Version="{version}" />
           </ItemGroup>
         </Project>
         """;

    private static void RunDotnet(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("NSchema needs the .NET SDK ('dotnet') on your PATH to restore plugins, but it could not be found.", ex);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Restoring an NSchema plugin failed (dotnet exit code {process.ExitCode}):{Environment.NewLine}{output.GetAwaiter().GetResult()}{error.GetAwaiter().GetResult()}");
        }
    }

    // The contract surface is NSchema.Core's entire public API, frozen within a major version, so a plugin built
    // against a different Core major would bind against types this host cannot supply. Fail clearly up front.
    private static void VerifyCoreCompatibility(string packageId, Assembly plugin)
    {
        var hostMajor = typeof(INSchemaPlugin).Assembly.GetName().Version?.Major;
        var pluginMajor = plugin.GetReferencedAssemblies()
            .FirstOrDefault(a => string.Equals(a.Name, "NSchema.Core", StringComparison.Ordinal))?.Version?.Major;

        if (pluginMajor != hostMajor)
        {
            throw new InvalidOperationException(
                $"Plugin '{packageId}' targets NSchema.Core v{pluginMajor?.ToString() ?? "unknown"}, but this CLI hosts NSchema.Core v{hostMajor}. A plugin must share the host's major version.");
        }
    }

    private sealed class PluginLoadContext(string entryAssemblyPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(entryAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Defer the shared contract and framework assemblies to the host's (default) context so that types such
            // as ISchemaProvider are the SAME Type on both sides of the boundary; isolate everything else (Npgsql,
            // the AWS SDK, ...) within this context.
            if (IsSharedWithHost(assemblyName.Name))
            {
                return null;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        private static bool IsSharedWithHost(string? name) =>
            name is "NSchema.Core"
            || (name?.StartsWith("System.", StringComparison.Ordinal) ?? false)
            || (name?.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ?? false);
    }
}
