using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Apply;

internal sealed class ApplyConfigurationValidator : AbstractValidator<ApplyConfiguration>
{
    public ApplyConfigurationValidator()
    {
        RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator());

        // Apply writes to a live database, so a provider is mandatory — not merely "at most one".
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for apply. Configure one in nschema.json, via --provider/--connection-string, or {EnvironmentVariables.ConnectionString}.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
