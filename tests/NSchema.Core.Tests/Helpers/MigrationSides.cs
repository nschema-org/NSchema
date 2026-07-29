using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Plan.Domain.Services;

namespace NSchema.Tests.Helpers;

/// <summary>
/// The two databases a plan's ordering reads — what is there now and what the project declares — built one
/// object at a time alongside the diff nodes under test, the way the comparer produces both together.
/// </summary>
internal sealed class MigrationSides
{
    private readonly Database _current = new() { Schemas = [] };
    private readonly Database _desired = new() { Schemas = [] };

    /// <summary>The edges the linearizer orders by.</summary>
    public PlanDependencies Dependencies => new(_current, _desired);

    /// <summary>Declares an object the project has and the database does not.</summary>
    public T Creating<T>(SqlIdentifier schema, T declared) where T : DatabaseObject => Declare(_desired, schema, declared);

    /// <summary>Declares an object the database has and the project does not.</summary>
    public T Dropping<T>(SqlIdentifier schema, T existing) where T : DatabaseObject => Declare(_current, schema, existing);

    private static T Declare<T>(Database side, SqlIdentifier schema, T declared) where T : DatabaseObject
    {
        var target = side.Schemas.FirstOrDefault(s => s.Name == schema);
        if (target is null)
        {
            target = new Schema { Name = schema };
            side.Schemas.Add(target);
        }

        switch (declared)
        {
            case Table table: target.Tables.Add(table); break;
            case View view: target.Views.Add(view); break;
            default: throw new NotSupportedException($"Nothing declares a {declared.GetType().Name} yet.");
        }

        return declared;
    }
}
