using Microsoft.Extensions.Logging;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Produces the structured <see cref="DatabaseDiff"/> from a current and desired schema.
/// </summary>
internal sealed partial class DatabaseComparer(ILogger<DatabaseComparer> logger, SqlEquivalence equivalence) : IDatabaseComparer
{
    public DatabaseDiff Compare(AlignedDatabase current, Database desired)
    {
        LogBeginningComparison();

        var schemas = CompareSchemas(current.Database.Schemas, desired.Schemas, current.Renames);
        var extensions = CompareExtensions(current.Database.Extensions, desired.Extensions);

        LogComparisonComplete(schemas.Count);

        return new DatabaseDiff(schemas, extensions);
    }

    private List<SchemaDiff> CompareSchemas(IReadOnlyList<Schema> current, IReadOnlyList<Schema> desired, RenameLog renames)
    {
        var result = new List<SchemaDiff>();
        var (forDesired, forCurrent) = NamedEntityMatcher.Match(current, desired);

        for (var j = 0; j < current.Count; j++)
        {
            if (forCurrent[j] is not null)
            {
                LogSchemaExists(current[j].Name);
            }
            else
            {
                LogSchemaNotInDesired(current[j].Name);
                result.Add(BuildRemovedSchema(current[j]));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogSchemaNew(desired[i].Name);
                result.Add(BuildNewSchema(desired[i]));
            }
            else if (BuildModifiedSchema(matchingCurrent, desired[i], renames) is { } diff)
            {
                result.Add(diff);
            }
        }

        // The diff presents schemas ordered by name (tables likewise, within each schema).
        result.Sort((a, b) => a.Name.CompareTo(b.Name));
        return result;
    }

    /// <summary>
    /// The shared per-kind diffing skeleton: pairs current and desired objects via
    /// <see cref="NamedEntityMatcher.Match{T}"/>, treats an unmatched current object as removed, and builds an add for
    /// each unmatched desired object or delegates to <paramref name="buildModified"/> for a pair (passing the
    /// name the entity was renamed from, when the alignment moved it). Only the per-kind build logic varies;
    /// the matching — and the name ordering the diff presents every kind in — lives here, once.
    /// </summary>
    private static List<TDiff> CompareObjects<TModel, TDiff>(
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        Func<SqlIdentifier, SqlIdentifier?> renamedFrom,
        Func<TModel, TDiff> buildRemoved,
        Func<TModel, TDiff> buildNew,
        Func<TModel, TModel, SqlIdentifier?, TDiff?> buildModified
    ) where TModel : DatabaseElement where TDiff : class, INamedObjectDiff
    {
        var result = new List<TDiff>();
        var (forDesired, forCurrent) = NamedEntityMatcher.Match(current, desired);

        for (var j = 0; j < current.Count; j++)
        {
            if (forCurrent[j] is null)
            {
                result.Add(buildRemoved(current[j]));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (forDesired[i] is not { } matchingCurrent)
            {
                result.Add(buildNew(desired[i]));
            }
            else if (buildModified(matchingCurrent, desired[i], renamedFrom(desired[i].Name)) is { } diff)
            {
                result.Add(diff);
            }
        }

        return result.OrderBy(d => d.Name).ToList();
    }

    /// <summary>
    /// The shared diffing skeleton for a table's list members (foreign keys, unique and check constraints,
    /// indexes): match by exact name — members don't rename — then a structurally changed or missing member is
    /// removed, a changed or new one is added (with its comment folded in as a trailing Modify), and a
    /// comment-only change is a Modify in place. Relies on <typeparamref name="TModel"/>'s equality
    /// <em>excluding</em> <see cref="NSchema.Model.DatabaseElement.Comment"/>, or the comment-only branch is unreachable.
    /// </summary>
    private List<TDiff> CompareTableMembers<TModel, TDiff>(
        ObjectAddress owner,
        string memberKind,
        IReadOnlyList<TModel> current,
        IReadOnlyList<TModel> desired,
        Func<TModel, TDiff> added,
        Func<SqlIdentifier, TDiff> removed,
        Func<SqlIdentifier, ValueChange<string>, TDiff> commentChanged
    ) where TModel : DatabaseElement
    {
        var result = new List<TDiff>();
        var currentByName = NamedEntityMatcher.FirstByName(current);
        var desiredByName = NamedEntityMatcher.FirstByName(desired);

        foreach (var currentMember in current)
        {
            if (!desiredByName.TryGetValue(currentMember.Name, out var matchingDesired)
                || !currentMember.Equals(matchingDesired))
            {
                LogTableMemberMissingOrChanged(memberKind, currentMember.Name, owner);
                result.Add(removed(currentMember.Name));
            }
            else if (currentMember.Comment != matchingDesired.Comment)
            {
                LogTableMemberCommentChanged(memberKind, currentMember.Name, owner);
                result.Add(commentChanged(currentMember.Name, new ValueChange<string>(currentMember.Comment, matchingDesired.Comment)));
            }
            else
            {
                LogTableMemberUnchanged(memberKind, currentMember.Name, owner);
            }
        }

        foreach (var desiredMember in desired)
        {
            if (!currentByName.TryGetValue(desiredMember.Name, out var matchingCurrent)
                || !matchingCurrent.Equals(desiredMember))
            {
                LogTableMemberNewOrChanged(memberKind, desiredMember.Name, owner);
                result.Add(added(desiredMember));
                if (desiredMember.Comment is not null)
                {
                    result.Add(commentChanged(desiredMember.Name, new ValueChange<string>(null, desiredMember.Comment)));
                }
            }
            else
            {
                LogTableMemberUnchanged(memberKind, desiredMember.Name, owner);
            }
        }

        return result;
    }

    private SchemaDiff BuildNewSchema(Schema desired)
    {
        var tables = desired.Tables
            .Select(table => BuildNewTable(desired.Name, table))
            .OrderBy(table => table.Name)
            .ToList();

        var views = desired.Views
            .Select(view => BuildNewView(desired.Name, view))
            .OrderBy(view => view.Name)
            .ToList();

        var enums = desired.Enums
            .Select(enumType => BuildNewEnum(desired.Name, enumType))
            .OrderBy(enumType => enumType.Name)
            .ToList();

        var sequences = desired.Sequences
            .Select(sequence => BuildNewSequence(desired.Name, sequence))
            .OrderBy(sequence => sequence.Name)
            .ToList();

        var routines = desired.Routines
            .Select(routine => BuildNewRoutine(desired.Name, routine))
            .OrderBy(routine => routine.Name)
            .ToList();

        var domains = desired.Domains
            .Select(domain => BuildNewDomain(desired.Name, domain))
            .OrderBy(domain => domain.Name)
            .ToList();

        var compositeTypes = desired.CompositeTypes
            .Select(type => BuildNewCompositeType(desired.Name, type))
            .OrderBy(type => type.Name)
            .ToList();

        var comment = desired.Comment is not null ? new ValueChange<string>(null, desired.Comment) : null;
        var grants = desired.Grants.Select(grant => new GrantChange(ChangeKind.Add, grant.Role, null)).ToList();

        // Don't create an implicit schema.
        var schema = desired.IsImplicit
            ? SchemaDiff.Containing(desired.Name)
            : SchemaDiff.Added(desired.Name);

        return schema with
        {
            Comment = comment,
            Grants = grants,
            Tables = tables,
            Views = views,
            Enums = enums,
            Sequences = sequences,
            Routines = routines,
            Domains = domains,
            CompositeTypes = compositeTypes,
        };
    }

    // A removed schema takes all of its objects with it. Rather than lean on a provider-specific DROP SCHEMA CASCADE
    // (which Postgres has but SQL Server and SQLite do not), emit an explicit Remove for every contained object — by
    // diffing the schema against an empty one — so the linearizer drops the objects before the schema itself.
    private SchemaDiff BuildRemovedSchema(Schema current)
    {
        var empty = new Schema { Name = current.Name };

        // Don't drop an implicit schema.
        var schema = current.IsImplicit
            ? SchemaDiff.Containing(current.Name)
            : SchemaDiff.Removed(current.Name);

        return schema with
        {
            Tables = CompareTables(current.Name, current.Tables, empty, RenameLog.Empty),
            Views = CompareViews(current.Name, current.Views, empty, RenameLog.Empty),
            Enums = CompareEnums(current.Name, current.Enums, empty, RenameLog.Empty),
            Sequences = CompareSequences(current.Name, current.Sequences, empty, RenameLog.Empty),
            Routines = CompareRoutines(current.Name, current.Routines, empty, RenameLog.Empty),
            Domains = CompareDomains(current.Name, current.Domains, empty, RenameLog.Empty),
            CompositeTypes = CompareCompositeTypes(current.Name, current.CompositeTypes, empty, RenameLog.Empty),
        };
    }

    private SchemaDiff? BuildModifiedSchema(Schema current, Schema desired, RenameLog renames)
    {
        var renamedFrom = renames.RenamedFrom(new SchemaAddress(desired.Name));
        if (renamedFrom is null)
        {
            LogSchemaUnchanged(desired.Name);
        }
        else
        {
            LogSchemaRenamed(renamedFrom, desired.Name);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogSchemaCommentChanged(desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var grants = CompareSchemaGrants(desired.Name, current.Grants, desired.Grants);
        var tables = CompareTables(desired.Name, current.Tables, desired, renames);
        var views = CompareViews(desired.Name, current.Views, desired, renames);
        var enums = CompareEnums(desired.Name, current.Enums, desired, renames);
        var sequences = CompareSequences(desired.Name, current.Sequences, desired, renames);
        var routines = CompareRoutines(desired.Name, current.Routines, desired, renames);
        var domains = CompareDomains(desired.Name, current.Domains, desired, renames);
        var compositeTypes = CompareCompositeTypes(desired.Name, current.CompositeTypes, desired, renames);

        // The schema entity itself only changes when it is renamed or its comment/grants change; a schema that
        // merely contains changed tables or views has a null Kind.
        var schemaLevelChange = renamedFrom is not null || comment is not null || grants.Count > 0;
        if (!schemaLevelChange && tables.Count == 0 && views.Count == 0 && enums.Count == 0 && sequences.Count == 0
            && routines.Count == 0 && domains.Count == 0 && compositeTypes.Count == 0)
        {
            return null;
        }

        var schema = schemaLevelChange ? SchemaDiff.Modified(desired.Name) : SchemaDiff.Containing(desired.Name);
        return schema with
        {
            RenamedFrom = renamedFrom,
            Comment = comment,
            Grants = grants,
            Tables = tables,
            Views = views,
            Enums = enums,
            Sequences = sequences,
            Routines = routines,
            Domains = domains,
            CompositeTypes = compositeTypes,
        };
    }
}
