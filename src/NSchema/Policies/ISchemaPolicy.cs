using NSchema.Schema;

namespace NSchema.Policies;

/// <summary>
/// Defines an interface for validating a database schema against a set of rules or policies.
/// </summary>
public interface ISchemaPolicy
{
    /// <summary>
    /// Validates the given database schema against the rules defined by this policy and returns a collection of any errors found during validation.
    /// </summary>
    /// <param name="schema">The database schema to validate against this policy.</param>
    /// <returns>The collection of errors found during validation. If the schema is valid according to this policy, the collection will be empty.</returns>
    IEnumerable<PolicyError> Validate(DatabaseSchema schema);
}
