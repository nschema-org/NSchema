using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Backends;
using NSchema.Diff.Domain.Services;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Round-trip drift gate: writing the desired schema out and reading it straight back must produce
/// <em>no</em> diff. This is the DB-free analogue of <c>import</c> → <c>plan</c> (and, once state captures
/// the applied schema, <c>apply</c> → <c>plan</c>): the trust contract that a clean cycle never shows a
/// phantom change. It runs the round-trip <em>through the comparer</em> — the component that actually
/// decides drift — rather than asserting structural equality, so it is the regression gate for the
/// serialization-fidelity bugs (e.g. <c>integer</c> vs <c>int</c>) that previously surfaced as drift.
/// </summary>
public sealed class RoundTripDriftTests
{
    private readonly DatabaseComparer _comparer = new(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence());

    [Fact]
    public void NsqlRoundTrip_OfRichSchema_ProducesNoDiff()
    {
        // Serialize every domain feature to DDL and read it straight back: the comparer must see no change.
        var original = TestData.RichSchema();
        var reparsed = new TestNsqlParser(NsqlWriter.Write(original)).Parse().Database;

        _comparer.Compare(AlignedDatabase.Unaligned(original), reparsed).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void NsqlRoundTrip_OfDirectives_IsFaithful()
    {
        // Arrange
        // Write the whole project — schema, and a directive of every kind — read it back, and write it
        // again: the second rendering must be byte-identical, so nothing is lost or reshaped in flight.
        var first = NsqlWriter.Write(TestData.RichSchema(), TestData.RichDirectives());

        var read = NsqlReader.Read(first);
        read.IsSuccess.ShouldBeTrue();
        var assembled = NSchema.Project.ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue(string.Join("; ", assembled.Diagnostics.Select(d => d.Message)));

        // Act
        var project = assembled.Value!;

        // Assert
        NsqlWriter.Write(project.Database, project.Directives).ShouldBe(first);
    }
}
