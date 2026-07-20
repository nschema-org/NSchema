using FluentValidation;

namespace NSchema.Commands.Db.Show;

internal sealed class DbShowConfigurationValidator : AbstractValidator<DbShowConfiguration>
{
    public DbShowConfigurationValidator()
    {
        // db show reads the live schema directly from the database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for db show: it reads the live schema directly from the database. Declare a DATABASE statement in a configuration (*.env.sql) file.");
    }
}
