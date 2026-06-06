using FluentValidation;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Commands.Validate;

internal sealed class ValidateConfigurationValidator : AbstractValidator<ValidateConfiguration>
{
    public ValidateConfigurationValidator()
    {
        // Validate checks the desired schema only: a directory is the single requirement, with no current-schema source.
        RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator());
    }
}
