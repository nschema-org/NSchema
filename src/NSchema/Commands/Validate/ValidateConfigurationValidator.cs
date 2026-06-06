using FluentValidation;
using NSchema.Configuration.Schema;

namespace NSchema.Commands.Validate;

internal sealed class ValidateConfigurationValidator : AbstractValidator<ValidateConfiguration>
{
    public ValidateConfigurationValidator()
    {
        // Validate checks the desired schema only: a directory is the single requirement, with no current-schema source.
        RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator());
    }
}
