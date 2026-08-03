using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Tables;
using NSchema.Model.Types;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Checks that every type the desired database references will exist once the plan applies.
/// </summary>
internal static class TypeReachability
{
    public static IEnumerable<Diagnostic> Check(DatabaseDiff diff, Database desired, Database current, SqlEquivalence equivalence)
    {
        // An engine that does not validate type names resolves every reference by definition.
        if (!equivalence.ValidatesTypeNames)
        {
            return [];
        }

        var (before, after) = TypeSets(diff, desired, current, equivalence);
        var misses = References(desired).Where(r => !after.Resolves(r.Type)).ToList();
        if (misses.Count == 0)
        {
            return [];
        }

        // Strictness is earned by capture. Without a vocabulary, a bare name resolving against the
        // engine's own types cannot be judged at all; only a reference that names its target outright is
        // checkable — unless the miss is the plan's own removal, which is knowable regardless.
        var hasVocabulary = current.Objects<NativeType>().Any();
        var removed = misses.Where(r => before.Resolves(r.Type)).ToList();
        var missing = misses.Where(r => !before.Resolves(r.Type) && (hasVocabulary || r.Type.Schema is not null)).ToList();

        // What an extension installed by this very run provides cannot be known until it exists, so every
        // miss softens to a hedge rather than guessing silently.
        if (diff.Extensions.Where(e => e.Change == ChangeKind.Add).Select(e => e.Name).ToList() is { Count: > 0 } installs)
        {
            var hedged = removed.Concat(missing).ToList();
            return hedged.Count > 0
                ? [DiffDiagnostics.TypeMayComeFromExtension(Dependents(hedged), Types(hedged), installs)]
                : [];
        }

        var diagnostics = new List<Diagnostic>();
        if (removed.Count > 0)
        {
            diagnostics.Add(DiffDiagnostics.RemovedTypeStillReferenced(Dependents(removed), Types(removed)));
        }
        if (missing.Count > 0)
        {
            diagnostics.Add(hasVocabulary
                ? DiffDiagnostics.UnresolvedTypes(Dependents(missing), Types(missing))
                : DiffDiagnostics.UnverifiedTypes(Dependents(missing), Types(missing)));
        }
        return diagnostics;

        static List<Address> Dependents(IEnumerable<(Address Dependent, SqlType Type)> misses) =>
            [.. misses.Select(r => r.Dependent).Distinct()];

        static List<string> Types(IEnumerable<(Address Dependent, SqlType Type)> misses) =>
            [.. misses.Select(r => Strip(r.Type).ToString()).Distinct()];
    }

    /// <summary>
    /// The types that exist around the plan: before it applies, and after — what the desired side
    /// declares, plus what the current database has that the plan does not remove, directly or by
    /// removing the providing extension. A miss on the after side that resolves on the before side is
    /// the plan's own removal.
    /// </summary>
    private static (TypeSet Before, TypeSet After) TypeSets(DatabaseDiff diff, Database desired, Database current, SqlEquivalence equivalence)
    {
        var removedExtensions = diff.Extensions
            .Where(e => e.Change == ChangeKind.Remove)
            .Select(e => e.Name)
            .ToHashSet();

        var removedTypes = diff.Schemas
            .SelectMany(schema => schema.Enums.Cast<ISchemaObjectDiff>()
                .Concat(schema.Domains)
                .Concat(schema.CompositeTypes)
                .Where(o => o.Change == ChangeKind.Remove)
                .Select(o => (Schema: schema.Name, o.Name)))
            .ToHashSet();

        var before = new List<SqlType>();
        var after = new List<SqlType>();

        foreach (var (schema, type) in desired.Objects<TypeObject>())
        {
            before.Add(Entry(schema, type.Name));
            after.Add(Entry(schema, type.Name));
        }

        // A declared type survives unless the plan removes it; a native type unless the plan removes the
        // extension that provides it.
        foreach (var (schema, type) in current.Objects<TypeObject>())
        {
            before.Add(Entry(schema, type.Name));
            var survives = type is NativeType native
                ? native.ProvidedBy is not { } provider || !removedExtensions.Contains(provider.Name)
                : !removedTypes.Contains((schema, type.Name));
            if (survives)
            {
                after.Add(Entry(schema, type.Name));
            }
        }

        return (new TypeSet(before, equivalence), new TypeSet(after, equivalence));

        static SqlType Entry(SqlIdentifier schema, SqlIdentifier name) => new(name) { Schema = schema };
    }

    /// <summary>
    /// Every type reference the desired database holds, with what holds it.
    /// </summary>
    private static IEnumerable<(Address Dependent, SqlType Type)> References(Database desired)
    {
        foreach (var (schema, table) in desired.Objects<Table>())
        {
            foreach (var column in table.Columns)
            {
                yield return (new MemberAddress(schema, table.Name, column.Name), column.Type);
            }
        }

        foreach (var (schema, domain) in desired.Objects<DomainType>())
        {
            yield return (new ObjectAddress(schema, domain.Name), domain.DataType);
        }

        foreach (var (schema, composite) in desired.Objects<CompositeType>())
        {
            foreach (var field in composite.Fields)
            {
                yield return (new ObjectAddress(schema, composite.Name), field.DataType);
            }
        }
    }

    /// <summary>
    /// The reference without its per-use facets: existence is a question about the name alone.
    /// </summary>
    private static SqlType Strip(SqlType reference) => new(reference.Name) { Schema = reference.Schema };

    /// <summary>
    /// Membership under the provider's equivalence, with a bare reference also matching an entry of the same
    /// name in any schema — the engine's search path resolves what the model does not qualify.
    /// </summary>
    private sealed class TypeSet
    {
        private readonly HashSet<SqlType> _known;
        private readonly HashSet<SqlIdentifier> _names;

        public TypeSet(IEnumerable<SqlType> entries, SqlEquivalence equivalence)
        {
            _known = new HashSet<SqlType>(equivalence.Types);
            _names = [];
            foreach (var entry in entries)
            {
                _known.Add(entry);
                _names.Add(entry.Name);
            }
        }

        public bool Resolves(SqlType reference)
        {
            var stripped = Strip(reference);
            return _known.Contains(stripped) || (stripped.Schema is null && _names.Contains(stripped.Name));
        }
    }
}
