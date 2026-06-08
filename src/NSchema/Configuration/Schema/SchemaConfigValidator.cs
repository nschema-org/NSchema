using FluentValidation;

namespace NSchema.Configuration.Schema;

internal sealed class SchemaConfigValidator : AbstractValidator<SchemaConfig>
{
    public SchemaConfigValidator()
    {
        RuleFor(x => x.Directory)
            .NotEmpty()
            .WithMessage("No schema directory configured. Set \"schema.dir\" in nschema.json.");

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("schema.format must be either yaml or json.");
    }
}
