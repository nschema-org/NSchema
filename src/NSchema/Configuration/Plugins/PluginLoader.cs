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

    // A cold restore (dotnet publish of the full dependency closure) can take tens of seconds;
    // under contention every waiter but the first only blocks for that one restore.
    private static readonly TimeSpan _restoreLockTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Exposes the plugin cache to check for available plugins.
    /// </summary>
    public PluginCache Cache { get; } = new(cacheRoot);

    /// <summary>
    /// Loads the plugin(s) the given package contains at the given version, restoring the package on first use.
    /// </summary>
    /// <param name="packageId">The plugin's NuGet package id.</param>
    /// <param name="version">The pinned version, keying the cache layout.</param>
    /// <param name="restoreVersion">The version in NuGet notation for the restore; defaults to <paramref name="version"/>.</param>
    /// <param name="allowRestore">Whether a missing plugin may be restored, or must already be cached.</param>
    public Result<IReadOnlyList<INSchemaPlugin>> Load(string packageId, string version, string? restoreVersion = null, bool allowRestore = true)
    {
        var published = EnsurePublished(packageId, version, restoreVersion ?? version, allowRestore);
        if (published.IsFailure)
        {
            return Result.Failure<IReadOnlyList<INSchemaPlugin>>(published.Diagnostics);
        }

        // Loading a third-party assembly through the isolated context is a genuine external-code boundary: a malformed
        // package, a type that won't instantiate, or a resolver failure surface as reflection/ALC exceptions. This is
        // the right place to catch — convert the failure to a diagnostic rather than let it escape unframed.
        try
        {
            var context = new PluginLoadContext(Path.Combine(published.Value, SynthAssemblyName + ".dll"));
            var assembly = context.LoadFromAssemblyName(new AssemblyName(packageId));

            // The engine handshake rejects a plugin built against an incompatible NSchema.Core before any of its
            // types are instantiated.
            var handshake = PluginHandshake.Validate(assembly);
            if (handshake.IsFailure)
            {
                return Result.Failure<IReadOnlyList<INSchemaPlugin>>(handshake.Diagnostics);
            }

            var plugins = assembly.GetExportedTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(INSchemaPlugin).IsAssignableFrom(t))
                .Select(t => (INSchemaPlugin)Activator.CreateInstance(t)!)
                .ToList();

            if (plugins.Count == 0)
            {
                return Diagnostic.Error(packageId, $"The package '{packageId}' does not contain an NSchema plugin.");
            }

            return Result.Success<IReadOnlyList<INSchemaPlugin>>(plugins);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Diagnostic.Error(packageId, $"The plugin '{packageId}' {version} could not be loaded: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the latest available version of <paramref name="packageId"/> that shares this host's NSchema.Core
    /// major version, by floating-restoring it and reading the resolved version back from the lock file. Used when
    /// scaffolding, where no version is pinned yet and a single failure aborts the command.
    /// </summary>
    public string ResolveLatestVersion(string packageId)
    {
        var hostMajor = typeof(INSchemaPlugin).Assembly.GetName().Version?.Major
            ?? throw new InvalidOperationException("Could not determine the host NSchema.Core major version.");

        var projectDir = Cache.ResolveDirectory(packageId);
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, SynthAssemblyName + ".csproj");

        // Concurrent runs share this scratch dir, so serialize the restore across processes.
        using var resolveLock = FileLock.Acquire(Cache.ResolveLockFile(packageId), _restoreLockTimeout)
            ?? throw new InvalidOperationException($"Timed out waiting for another process to finish resolving the latest version of plugin '{packageId}'.");

        // Float within the host major (prereleases included) so the pin always matches a plugin this CLI can load.
        File.WriteAllText(projectPath, SynthProject(packageId, $"{hostMajor}.*-*"));

        // --force re-evaluates the floating range every time, so a newly published version is picked up.
        RunDotnet("restore", projectPath, "--force").Require();

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

    private Result<string> EnsurePublished(string packageId, string version, string restoreVersion, bool allowRestore)
    {
        var publishDir = Cache.PublishDirectory(packageId, version);
        var pluginAssembly = Cache.PluginAssembly(packageId, version);

        // Fast path: a completed publish closure is reused across runs.
        if (File.Exists(pluginAssembly))
        {
            return publishDir;
        }

        // --no-init: stay offline and require the plugin to be cached already.
        if (!allowRestore)
        {
            return Diagnostic.Error(packageId,
                $"Plugin '{packageId}' {version} is not restored, and --no-init was specified. Run 'nschema init' (or drop --no-init) to restore it first.");
        }

        // Concurrent runs share this cache, so serialize the restore of a given version across processes.
        Directory.CreateDirectory(Cache.VersionDirectory(packageId, version));
        using var restoreLock = FileLock.Acquire(Cache.LockFile(packageId, version), _restoreLockTimeout);
        if (restoreLock is null)
        {
            return Diagnostic.Error(packageId, $"Timed out waiting for another process to finish restoring plugin '{packageId}' {version}.");
        }

        // Re-check under the lock: another process may have completed the restore while we were waiting for it.
        if (File.Exists(pluginAssembly))
        {
            return publishDir;
        }

        var projectDir = Cache.ProjectDirectory(packageId, version);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, SynthAssemblyName + ".csproj"), SynthProject(packageId, restoreVersion));

        // Publish into a staging dir and atomically rename it onto the publish dir, so the lock-free fast path above
        // never sees a partially-populated closure.
        var staging = Cache.StagingDirectory(packageId, version);
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        var publish = RunDotnet("publish", Path.Combine(projectDir, SynthAssemblyName + ".csproj"), "-c", "Release", "-o", staging);
        if (publish.IsFailure)
        {
            return Result.Failure<string>(publish.Diagnostics);
        }

        if (!File.Exists(Path.Combine(staging, packageId + ".dll")))
        {
            return Diagnostic.Error(packageId,
                $"Restored package '{packageId}' {version} but its assembly '{packageId}.dll' was not found — is the package an NSchema plugin?");
        }

        // A leftover from an interrupted earlier restore would block the rename; we hold the lock, so clearing it is safe.
        if (Directory.Exists(publishDir))
        {
            Directory.Delete(publishDir, recursive: true);
        }

        Directory.Move(staging, publishDir);

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

    private static Result<int> RunDotnet(params string[] args)
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

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Win32Exception)
        {
            // Launching the external 'dotnet' process is a genuine external-code boundary, so catching is correct here.
            return Result.From(process?.ExitCode ?? -1, [Diagnostic.Error("dotnet",
                "NSchema needs the .NET SDK ('dotnet') on your PATH to restore plugins, but it could not be found.")]);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return Result.From(process.ExitCode, [Diagnostic.Error("dotnet",
                $"Restoring an NSchema plugin failed (dotnet exit code {process.ExitCode}):{Environment.NewLine}{output.GetAwaiter().GetResult()}{error.GetAwaiter().GetResult()}")]);
        }

        return Result.Success(0);
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

        // A plugin's dependency closure can carry native libraries (e.g. SQLite's e_sqlite3); resolve them from the
        // same publish closure the managed assemblies load from.
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }

        private static bool IsSharedWithHost(string? name) =>
            name is "NSchema.Core"
            || (name?.StartsWith("System.", StringComparison.Ordinal) ?? false)
            || (name?.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ?? false);
    }
}
