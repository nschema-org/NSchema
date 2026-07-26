using NSchema.Project.Nsql;

namespace NSchema.Plugins;

/// <summary>
/// The contract common to every NSchema plugin.
/// </summary>
/// <remarks>
/// Implement one of the derived interfaces, never this one directly.
/// </remarks>
public interface INSchemaPlugin
{
    /// <summary>
    /// The questions to put to the user before scaffolding, in the order they should be asked.
    /// Empty (the default) scaffolds placeholders instead, which is what a non-interactive run gets.
    /// </summary>
    /// <param name="context">Describes what is being scaffolded (e.g. the target environment).</param>
    IReadOnlyList<ScaffoldPrompt> GetScaffoldPrompts(ScaffoldContext context) => [];

    /// <summary>
    /// Builds the starter configuration this plugin contributes when a new project is scaffolded.
    /// </summary>
    /// <param name="context">Describes what is being scaffolded: the target environment, and any answers given.</param>
    NsqlDocument GetScaffoldTemplate(ScaffoldContext context);
}
