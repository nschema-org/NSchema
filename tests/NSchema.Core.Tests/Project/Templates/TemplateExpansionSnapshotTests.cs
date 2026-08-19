using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Templates;

/// <summary>
/// Snapshot coverage for <see cref="TemplateApplicator"/>: the expanded schema is rendered back to DDL with
/// <see cref="NSchema.Project.Nsql.NsqlWriter"/>, so the snapshot shows exactly what the rest of the pipeline sees per target schema —
/// placeholder foreign keys re-pointed, and template-declared types and trigger functions qualified per instance.
/// </summary>
public sealed class TemplateExpansionSnapshotTests
{
    [Fact]
    public Task Expand_OutboxTemplate_IntoTwoSchemas()
    {
        // Arrange
        const string source =
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;

            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at timestamptz NOT NULL,
              updated_at timestamptz NOT NULL,
              CONSTRAINT chk_audit CHECK (updated_at >= created_at)
            END;

            TEMPLATE outbox
            BEGIN
              CREATE ENUM outbox_status ('pending', 'sent');
              CREATE FUNCTION outbox_notify() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END; $$;
              CREATE TABLE events (
                id uuid NOT NULL,
                status outbox_status NOT NULL,
                payload text NOT NULL,
                INCLUDE audit_columns,
                CONSTRAINT pk_events PRIMARY KEY (id)
              );
              CREATE TABLE event_locks (
                event_id uuid NOT NULL,
                CONSTRAINT fk_event FOREIGN KEY (event_id) REFERENCES events (id),
                CONSTRAINT fk_owner FOREIGN KEY (event_id) REFERENCES public.owners (id)
              );
              CREATE TRIGGER trg_notify AFTER INSERT ON events FOR EACH ROW EXECUTE FUNCTION outbox_notify();
              GRANT SELECT, INSERT ON events TO svc;
            END;

            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """;

        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();

        // Act
        var expanded = NSchema.Project.ProjectAssembler.Assemble([read.Value]).Require().Database;

        // Assert
        return Verify(NsqlWriter.Write(expanded));
    }
}
