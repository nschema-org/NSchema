using NSchema.Configuration.Domain;
using NSchema.Project.Nsql;

namespace NSchema.Configuration.Engine;

/// <summary>
/// The diagnostics minted when the project's <c>ENGINE</c> assertion is not satisfied.
/// </summary>
internal static class EngineDiagnostics
{
    private const string Source = "config";

    /// <summary>
    /// The engine's version falls outside the range the project's <c>ENGINE</c> statement requires.
    /// </summary>
    public static NsqlDiagnostic EngineRequirementUnsatisfied(VersionRange range, SemanticVersion version) =>
        new(Source, $"This project requires an engine version matching '{range}', but this engine is {version}. Update the engine, or relax the ENGINE assertion.", DiagnosticSeverity.Error, SourcePosition.None);

    /// <summary>
    /// The host tool's version falls outside the range the project's <c>ENGINE</c> statement requires.
    /// </summary>
    public static NsqlDiagnostic HostRequirementUnsatisfied(VersionRange range, SemanticVersion version) =>
        new(Source, $"This project requires a host version matching '{range}', but this host is {version}. Update the tool, or relax the ENGINE assertion.", DiagnosticSeverity.Error, SourcePosition.None);
}
