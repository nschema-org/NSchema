using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Project;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a project source: the files matching <paramref name="globPattern"/> under <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The directory the glob is matched against.</param>
    /// <param name="globPattern">A glob pattern relative to <paramref name="baseDirectory"/>. Defaults to <c>**/*.sql</c> (every SQL file, recursively). A wildcard-free pattern names a single file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddProjectSource(string baseDirectory, string globPattern = "**/*.sql")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globPattern);
        var matcher = new Matcher();
        matcher.AddInclude(globPattern);
        return AddProjectSource(baseDirectory, matcher);
    }

    /// <summary>
    /// Adds a project source: the files the given <see cref="Matcher"/> selects under <paramref name="baseDirectory"/>.
    /// May be called more than once (e.g. a base set plus an environment overlay); the sources are aggregated.
    /// </summary>
    /// <param name="baseDirectory">The directory the matcher is run against.</param>
    /// <param name="matcher">A configured glob matcher (includes and optional excludes), matched relative to <paramref name="baseDirectory"/>.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddProjectSource(string baseDirectory, Matcher matcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(matcher);

        Services.AddSingleton(new ProjectSource(baseDirectory, matcher));
        return this;
    }
}
