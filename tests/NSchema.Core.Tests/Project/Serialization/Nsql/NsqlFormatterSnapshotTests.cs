using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Snapshot coverage for <see cref="NsqlWriter"/> over the template statement family.
/// </summary>
public sealed class NsqlFormatterSnapshotTests
{
    [Fact]
    public Task Format_TemplateDocument()
    {
        // A deliberately messy document: a template holding several statement kinds (with comments), an
        // application, and surrounding ordinary statements — the snapshot pins the canonical layout.
        const string source =
            """
            create schema billing;
              create schema ordering;
            --- Standard audit columns for every table.
            template audit_columns for table begin
                created_at datetimeoffset not null,
              updated_at datetimeoffset not null,  -- touched on write
                  constraint chk_audit check (updated_at >= created_at)
             end;
            --- Transactional outbox, one per subdomain schema.
            template outbox begin
                create enum outbox_status('pending','sent');
              create table outbox(
                  id uuid not null,
                status outbox_status not null,  -- current delivery state
                    payload text not null,
                  include audit_columns,
                constraint pk_outbox primary key(id));
              -- covering index for the dispatcher
             create index ix_outbox_status on outbox(status);
                grant select, insert on outbox to svc;
                end;
                apply template outbox in schema billing,   ordering;
            """;

        return Verify(NsqlWriter.Format(source).Value);
    }

    [Fact]
    public Task Format_ScriptDocument()
    {
        // A messy document of unified SCRIPT statements across both event kinds and every run condition —
        // the snapshot pins the canonical layout.
        const string source =
            """
            create schema app;
            SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$
            CREATE EXTENSION IF NOT EXISTS citext;
            $$;
              script seed_currencies run once on post deployment as $$
            INSERT INTO app.currencies VALUES ('GBP');
            $$;
            --- Backfill the new email column from the legacy table.
            SCRIPT backfill_emails RUN ALWAYS ON ADD COLUMN app.users.email (run_outside_transaction = true) AS $$
            UPDATE app.users u SET email = l.email FROM legacy.users l WHERE l.id = u.id;
            $$;
            """;

        return Verify(NsqlWriter.Format(source).Value);
    }

    [Fact]
    public Task Format_MigrationDocument()
    {
        // A messy document mixing SCRIPT change-event blocks (with options) into ordinary statements —
        // the snapshot pins the canonical layout: dollar bodies verbatim, one blank line between statements.
        const string source =
            """
            create schema app;
              create table app.users(
                id bigint not null identity,
              email text not null,
                constraint users_pkey primary key(id));
            --- Backfill the new email column from the legacy table.
            SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users u SET email = l.email FROM legacy.users l WHERE l.id = u.id;
            $$;
               script noop_ids run on alter column type app.users.id (run_outside_transaction = true) as $$
            UPDATE app.users SET id = id;
            $$;
            """;

        return Verify(NsqlWriter.Format(source).Value);
    }
}
