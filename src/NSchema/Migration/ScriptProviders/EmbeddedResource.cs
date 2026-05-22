using System.Reflection;

namespace NSchema.Migration.ScriptProviders;

internal static class EmbeddedResource
{
    public static async Task<string> Read(Assembly assembly, string resourceName, CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static string DeriveName(string resourceName)
    {
        int lastDot = resourceName.LastIndexOf('.');
        int secondLastDot = resourceName.LastIndexOf('.', lastDot - 1);
        return secondLastDot >= 0
            ? resourceName[(secondLastDot + 1)..lastDot]
            : resourceName[..lastDot];
    }
}
