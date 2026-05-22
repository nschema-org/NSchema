using System.Reflection;

namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Provides utility methods for reading embedded resources from an assembly, such as migration scripts.
/// </summary>
internal static class EmbeddedResource
{
    /// <summary>
    /// Reads the content of an embedded resource from the specified assembly asynchronously.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="resourceName">The name of the embedded resource to read.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task containing the content of the embedded resource as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified embedded resource is not found in the assembly.</exception>
    public static async Task<string> Read(Assembly assembly, string resourceName, CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Derives a simple name for an embedded resource by extracting the portion of the resource name between the last two dots.
    /// </summary>
    /// <param name="resourceName">The full name of the embedded resource, typically in the format "Namespace.SubNamespace.ResourceName.Extension".</param>
    /// <returns>A simplified name for the embedded resource, extracted from the full resource name.</returns>
    public static string DeriveName(string resourceName)
    {
        int lastDot = resourceName.LastIndexOf('.');
        int secondLastDot = resourceName.LastIndexOf('.', lastDot - 1);
        return secondLastDot >= 0
            ? resourceName[(secondLastDot + 1)..lastDot]
            : resourceName[..lastDot];
    }
}
