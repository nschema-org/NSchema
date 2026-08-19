using NSchema.Model.Scripts;

namespace NSchema.Project.Nsql;

/// <summary>
/// The scripts projected from a document or template body, kept in their kind buckets so the pipeline never
/// holds a mixed <c>Script</c> collection — each stage adds and reads the concrete kind it means.
/// </summary>
internal sealed class ProjectedScripts
{
    public List<ChangeScript> Change { get; } = [];

    public List<DeploymentScript> Deployment { get; } = [];

    public void Add(ChangeScript script) => Change.Add(script);

    public void Add(DeploymentScript script) => Deployment.Add(script);

    public void AddRange(ProjectedScripts other)
    {
        Change.AddRange(other.Change);
        Deployment.AddRange(other.Deployment);
    }
}
