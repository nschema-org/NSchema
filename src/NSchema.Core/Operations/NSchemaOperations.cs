namespace NSchema.Operations;

/// <summary>
/// Aggregates the individual <see cref="IOperation{TArgs,TResult}"/> services behind the single <see cref="INSchemaOperations"/> surface.
/// </summary>
internal sealed class NSchemaOperations(
    IOperation<PlanArguments, Result<PlanResult>> plan,
    IOperation<ApplyArguments, Result<ApplyResult>> apply,
    IOperation<RefreshArguments, Result<RefreshResult>> refresh,
    IOperation<ValidateArguments, Result<ValidateResult>> validate,
    IOperation<DriftArguments, Result<DriftResult>> drift,
    IOperation<ImportArguments, Result<ImportResult>> import,
    IOperation<DoctorArguments, Result<DoctorResult>> doctor
) : INSchemaOperations
{
    public Task<Result<PlanResult>> Plan(PlanArguments args, CancellationToken cancellationToken = default) =>
        plan.Execute(args, cancellationToken);

    public Task<Result<ApplyResult>> Apply(ApplyArguments args, CancellationToken cancellationToken = default) =>
        apply.Execute(args, cancellationToken);

    public Task<Result<RefreshResult>> Refresh(RefreshArguments args, CancellationToken cancellationToken = default) =>
        refresh.Execute(args, cancellationToken);

    public Task<Result<ValidateResult>> Validate(ValidateArguments args, CancellationToken cancellationToken = default) =>
        validate.Execute(args, cancellationToken);

    public Task<Result<DriftResult>> Drift(DriftArguments args, CancellationToken cancellationToken = default) =>
        drift.Execute(args, cancellationToken);

    public Task<Result<ImportResult>> Import(ImportArguments args, CancellationToken cancellationToken = default) =>
        import.Execute(args, cancellationToken);

    public Task<Result<DoctorResult>> Doctor(DoctorArguments args, CancellationToken cancellationToken = default) =>
        doctor.Execute(args, cancellationToken);
}
