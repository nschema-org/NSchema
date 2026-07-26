using FluentValidation;

namespace NSchema.Commands.Database.Show;

internal sealed class DatabaseShowConfigurationValidator : AbstractValidator<DatabaseShowConfiguration>
{
    public DatabaseShowConfigurationValidator()
    {
        // db show reads the live schema directly from the database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for db show: it reads the live schema directly from the database. Declare a DATABASE statement.");
    }
}
