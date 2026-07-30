using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Extensions;

namespace NSchema.Diff.Domain.Extensions;

/// <summary>
/// Describes a change to a database extension.
/// </summary>
public sealed record ExtensionDiff : IDatabaseObjectDiff
{
    [JsonConstructor]
    private ExtensionDiff() { }

    /// <summary>
    /// The extension name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The extension's address.
    /// </summary>
    [JsonIgnore]
    public DatabaseAddress Address => DatabaseAddress.Extension(Name);

    /// <summary>
    /// The change to the database extension.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added database extension; otherwise <see langword="null"/>.
    /// </summary>
    public Extension? Definition { get; init; }

    /// <summary>
    /// The change to the extension's version, if any.
    /// </summary>
    public ValueChange<string>? Version { get; init; }

    /// <summary>
    /// The change to the database extension's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a extension being installed, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A database extension being created, named by its own definition.
    /// </summary>
    public static ExtensionDiff Added(Extension definition) => new()
    {
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A database extension being dropped.
    /// </summary>
    public static ExtensionDiff Removed(SqlIdentifier name) =>
        new() { Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A database extension altered in place; the individual changes are set on the result.
    /// </summary>
    public static ExtensionDiff Modified(SqlIdentifier name) =>
        new() { Name = name, Change = ChangeKind.Modify };
}
