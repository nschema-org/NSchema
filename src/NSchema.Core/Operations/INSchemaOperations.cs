namespace NSchema.Operations;

/// <summary>
/// Provides one discoverable surface over every NSchema workflow operation.
/// </summary>
public interface INSchemaOperations
{
    /// <summary>
    /// Computes a plan without applying it.
    /// </summary>
    Task<Result<PlanResult>> Plan(PlanArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a computed plan (from <see cref="Plan"/> or a saved plan file).
    /// </summary>
    Task<Result<ApplyResult>> Apply(ApplyArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live current schema and writes it to the state store.
    /// </summary>
    Task<Result<RefreshResult>> Refresh(RefreshArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the project and validates it against the configured project policies.
    /// </summary>
    Task<Result<ValidateResult>> Validate(ValidateArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares the recorded state against the live database and reports how the live database has drifted.
    /// </summary>
    Task<Result<DriftResult>> Drift(DriftArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live database schema and writes it as project source files.
    /// </summary>
    Task<Result<ImportResult>> Import(ImportArguments args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs read-only health checks against the configured infrastructure and reports the outcome of each.
    /// </summary>
    Task<Result<DoctorResult>> Doctor(DoctorArguments args, CancellationToken cancellationToken = default);
}
