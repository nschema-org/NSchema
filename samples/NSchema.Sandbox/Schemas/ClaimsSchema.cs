using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClaimsSchema : AbstractSchemaProvider
{
    public ClaimsSchema()
    {
        var claims = Schema("claims")
            .Comment("Schema for claim logging.")
            .Grant(Roles.Api);

        AddIntegrationEventsTable(claims);
        AddEventsTable(claims);
        AddProjectionsTable(claims);
        AddDamageSources(claims);
        AddPerils(claims);
        AddClaimTypes(claims);
        AddClaimStatuses(claims);
        AddClaimPriorities(claims);
        AddProjectionClaimSummaries(claims);
    }

    private static void AddIntegrationEventsTable(SchemaBuilder schema)
    {
        var table = schema.Table("integration_events")
            .Comment("Outbox table for integration events waiting to be published to the message broker.")
            .Grant(Roles.Api, TablePrivilege.All);

        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("integration_events_pkey");
        table.Column("event_type", SqlType.Text).NotNull().Comment("The fully qualified .NET type name of the integration event.");
        table.Column("payload", SqlType.Jsonb).NotNull().Comment("The event payload.");
        table.Column("created_at", SqlType.DateTimeOffset).NotNull().Comment("When the event was written to the outbox.");
        table.Column("published_at", SqlType.DateTimeOffset).Comment("When the event was published to the message broker. NULL if not yet published.");
        table.Column("metadata", SqlType.Jsonb).NotNull().Default("'{}'::jsonb")
            .Comment("Envelope metadata (originator, and any future fields like event version or profile id). Propagated to consumers via the x-abodio-metadata AMQP header.");
    }

    private static void AddEventsTable(SchemaBuilder schema)
    {
        var table = schema.Table("events")
            .Comment("Append-only event store for claims.")
            .Grant(Roles.Api, TablePrivilege.AppendOnly);

        table.Column("aggregate_id", SqlType.Text).NotNull().Comment("The claim ID this event belongs to.");
        table.Column("sequence_number", SqlType.Int).NotNull().Comment("Ordering of events within a single claim.");
        table.Column("global_sequence", SqlType.BigInt).NotNull().Identity(startWith: 0, minValue: 0).Comment("Monotonically increasing sequence across all aggregates. Used by projections for checkpointing.");
        table.Column("timestamp", SqlType.DateTimeOffset).NotNull().Comment("UTC timestamp when the event was recorded.");
        table.Column("event_type", SqlType.Text).NotNull().Comment("Discriminator used for deserialization.");
        table.Column("payload", SqlType.Jsonb).NotNull().Comment("JSON-serialized event data.");
        table.Column("user_id", SqlType.Text).Comment("The ID of the user who triggered this event. NULL for system events.");
        table.Column("user_name", SqlType.Text).Comment("The display name of the user who triggered this event. NULL for system events.");

        table.PrimaryKey("pk_claim_events", ["aggregate_id", "sequence_number"]);

        table.Index("ix_claim_events_global_sequence", ["global_sequence"]).Unique();
    }

    private static void AddProjectionsTable(SchemaBuilder schema)
    {
        var table = schema.Table("projections")
            .Comment("Stores the checkpoint for the last projected claim")
            .Grant(Roles.Api, TablePrivilege.All);

        table.Column("id", SqlType.Text).Comment("Projection id (value object); defined by each concrete projection handler, may encode a composite key as a single string").PrimaryKey("pk_projectionss");
        table.Column("last_processed_global_sequence", SqlType.BigInt).NotNull().Default("0").Comment("The sequence number of last projected claim");
        table.Column("last_updated", SqlType.DateTimeOffset).NotNull().Comment("When the checkpoint was last updated");
    }

    private static void AddDamageSources(SchemaBuilder schema)
    {
        var table = schema.Table("damage_sources")
            .Comment("Stores information about all damage sources that might cause a claim.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("damage_sources_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the damage source.");
        table.Column("description", SqlType.Citext).NotNull();
        table.Index("uc_damage_sources_name", ["name"]).Unique();
    }

    private static void AddPerils(SchemaBuilder schema)
    {
        var table = schema.Table("perils")
            .Comment("Stores information about all perils that might need to be resolved as part of a claim.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("perils_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the peril.");
        table.Column("description", SqlType.Citext).NotNull();
        table.Index("uc_perils_name", ["name"]).Unique();
    }

    private static void AddClaimTypes(SchemaBuilder schema)
    {
        var table = schema.Table("claim_types")
            .Comment("Stores information about all the different claim types.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("claim_types_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim type.");
        table.Column("description", SqlType.Citext).NotNull().Comment("A description of the claim type.");
        table.Column("covers_building", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the building.");
        table.Column("covers_contents", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the contents of a building.");
        table.Index("uc_claim_types_name", ["name"]).Unique();
    }

    private static void AddClaimStatuses(SchemaBuilder schema)
    {
        var table = schema.Table("claim_statuses")
            .Comment("Stores information about all the different claim statuses.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("claim_statuses_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim status.");
        table.Column("description", SqlType.Citext).NotNull();
        table.Index("uc_claim_statuses_name", ["name"]).Unique();
    }

    private static void AddClaimPriorities(SchemaBuilder schema)
    {
        var table = schema.Table("claim_priorities")
            .Comment("Stores information about all the different claim priority levels.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("claim_priorities_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim priority.");
        table.Column("description", SqlType.Citext).NotNull().Comment("A description of the claim priority.");
        table.Column("priority_order", SqlType.Int).NotNull().Default("0").Comment("An indicator of how important this priority is (lower is more important).");
        table.Column("color", SqlType.Citext).Comment("The color indicator used to denote claims of this priority level.");
        table.Index("uc_claim_priorities_name", ["name"]).Unique();
    }

    private static void AddProjectionClaimSummaries(SchemaBuilder schema)
    {
        var table = schema.Table("projection_claim_summaries")
            .Comment("Stores a projected view of the most up to date claim dashboard data")
            .Grant(Roles.Api, TablePrivilege.All);

        table.Column("id", SqlType.Text).PrimaryKey("projection_claim_summaries_pkey");
        table.Column("type_id", SqlType.Text).NotNull().Comment("The id for the claim type");
        table.Column("type_name", SqlType.Text).NotNull().Comment("The name for the claim type");
        table.Column("status_id", SqlType.Text).NotNull().Comment("The id for the claim status");
        table.Column("status_name", SqlType.Text).NotNull().Comment("The name for the claim status");
    }
}
