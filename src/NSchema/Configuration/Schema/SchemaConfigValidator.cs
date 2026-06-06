using FluentValidation;

namespace NSchema.Cli.Configuration.Schema;

internal sealed class SchemaConfigValidator : AbstractValidator<SchemaConfig>
{
    public SchemaConfigValidator()
    {
        RuleFor(x => x.Directory)
            .NotEmpty()
            .WithMessage("No schema directory configured. Set \"schema.dir\" in nschema.json or pass --schema-dir.");

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("schema.format must be either yaml or json.");
    }
}
