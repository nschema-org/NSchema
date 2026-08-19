using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Configuration;

/// <summary>
/// <c>RESTRICT</c> as a referential action in its own right.
/// </summary>
/// <remarks>
/// It used to fold into <c>NO ACTION</c> on the way in, which meant a foreign key declared to refuse a delete came
/// back declaring nothing of the sort — and every leg that compares NSchema to itself agreed, because both sides had
/// already lost it.
/// </remarks>
public sealed class ReferentialRestrictTests
{
    private const string Table =
        """
        CREATE TABLE app.orders (
          id int NOT NULL,
          customer_id int NOT NULL,
          CONSTRAINT pk_orders PRIMARY KEY (id),
          CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES app.customers (id)
            ON DELETE RESTRICT ON UPDATE CASCADE
        );
        """;

    [Fact]
    public void Restrict_SurvivesTheRoundTrip()
    {
        // Act
        var assembled = TestNsqlParser.Assemble([Table]);

        // Assert
        assembled.IsSuccess.ShouldBeTrue(string.Join("; ", assembled.Diagnostics.Select(d => d.Message)));

        var key = assembled.Require().Database.Schemas.Single().Tables.Single().ForeignKeys.Single();
        key.OnDelete.ShouldBe(ReferentialAction.Restrict);
        key.OnUpdate.ShouldBe(ReferentialAction.Cascade);
    }

    [Fact]
    public void Restrict_IsWrittenBackAsRestrict()
    {
        // A round trip that parses RESTRICT and writes NO ACTION would be lossless by the parser's own measure and
        // wrong by anyone else's, so the written text is what this asserts.

        // Arrange
        var read = NsqlReader.Read(Table);
        read.IsSuccess.ShouldBeTrue();

        // Act
        var written = NsqlWriter.Write(read.Value);

        // Assert
        written.ShouldContain("ON DELETE RESTRICT");
        written.ShouldContain("ON UPDATE CASCADE");
    }
}
