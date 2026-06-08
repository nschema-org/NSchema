using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

internal sealed class DestroyConfigurationValidator : AbstractValidator<DestroyConfiguration>
{
    public DestroyConfigurationValidator()
    {
        // Destroy generates and executes SQL against a live database, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for destroy. Configure one in nschema.json, via --provider/--connection-string, or {EnvironmentVariables.ConnectionString}.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        RuleFor(x => x.State).SetValidator(new StateConfigValidator());

        // The managed schema that gets torn down comes from the state store when one is configured, otherwise from
        // the desired schema — so at least one of the two must be present.
        RuleFor(x => x)
            .Must(c => c.State.ConfiguredSectionCount >= 1 || c.HasSchema)
            .WithMessage("Destroy needs a managed schema source: configure a state store, or a desired schema via \"schema.dir\"/--schema-dir to fall back on.");

        // Only validate the desired-schema shape when it's actually being used as the source.
        RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator()).When(x => x.HasSchema);
    }
}
