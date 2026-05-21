using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClaimsSchema : AbstractSchemaProvider
{
    public ClaimsSchema()
    {
        var claims = Schema("claims")
            .Comment("Schema for claim logging.")
            .Grant("abodio_api");

        var damageSources = claims.Table("damage_sources")
            .Comment("Stores information about all damage sources that might cause a claim.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        damageSources.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        damageSources.Column("name", SqlType.Custom("citext")).NotNull().Comment("The name of the damage source.");
        damageSources.Column("description", SqlType.Custom("citext")).NotNull();
        damageSources.PrimaryKey("pk_damage_sources", ["id"]);
        damageSources.Index("uc_damage_sources_name", ["name"]).Unique();

        var perils = claims.Table("perils")
            .Comment("Stores information about all perils that might need to be resolved as part of a claim.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        perils.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        perils.Column("name", SqlType.Custom("citext")).NotNull().Comment("The name of the peril.");
        perils.Column("description", SqlType.Custom("citext")).NotNull();
        perils.PrimaryKey("pk_perils", ["id"]);
        perils.Index("uc_perils_name", ["name"]).Unique();

        var claimTypes = claims.Table("claim_types")
            .Comment("Stores information about all the different claim types.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        claimTypes.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        claimTypes.Column("name", SqlType.Custom("citext")).NotNull().Comment("The name of the claim type.");
        claimTypes.Column("description", SqlType.Custom("citext")).NotNull().Comment("A description of the claim type.");
        claimTypes.Column("covers_building", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the building.");
        claimTypes.Column("covers_contents", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the contents of a building.");
        claimTypes.PrimaryKey("pk_claim_types", ["id"]);
        claimTypes.Index("uc_claim_types_name", ["name"]).Unique();

        var claimStatuses = claims.Table("claim_statuses")
            .Comment("Stores information about all the different claim statuses.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        claimStatuses.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        claimStatuses.Column("name", SqlType.Custom("citext")).NotNull().Comment("The name of the claim status.");
        claimStatuses.Column("description", SqlType.Custom("citext")).NotNull();
        claimStatuses.PrimaryKey("pk_claim_statuses", ["id"]);
        claimStatuses.Index("uc_claim_statuses_name", ["name"]).Unique();

        var claimPriorities = claims.Table("claim_priorities")
            .Comment("Stores information about all the different claim priority levels.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        claimPriorities.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        claimPriorities.Column("name", SqlType.Custom("citext")).NotNull().Comment("The name of the claim priority.");
        claimPriorities.Column("description", SqlType.Custom("citext")).NotNull().Comment("A description of the claim priority.");
        claimPriorities.Column("priority_order", SqlType.Int).NotNull().Default("0").Comment("An indicator of how important this priority is (lower is more important).");
        claimPriorities.Column("color", SqlType.Custom("citext")).Comment("The color indicator used to denote claims of this priority level.");
        claimPriorities.PrimaryKey("pk_claim_priorities", ["id"]);
        claimPriorities.Index("uc_claim_priorities_name", ["name"]).Unique();

        var events = claims.Table("events")
            .Comment("Append-only event store for claims.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert);
        events.Column("aggregate_id", SqlType.Custom("typeid")).NotNull().Comment("The claim ID this event belongs to.");
        events.Column("sequence_number", SqlType.Int).NotNull().Comment("Ordering of events within a single claim.");
        events.Column("global_sequence", SqlType.BigInt).NotNull().Identity(startWith: 0, minValue: 0).Comment("Monotonically increasing sequence across all aggregates.");
        events.Column("timestamp", SqlType.DateTimeOffset).NotNull().Comment("UTC timestamp when the event was recorded.");
        events.Column("event_type", SqlType.Text).NotNull().Comment("Discriminator used for deserialization.");
        events.Column("payload", SqlType.Custom("jsonb")).NotNull().Comment("JSON-serialized event data.");
        events.Column("user_id", SqlType.Text).Comment("The ID of the user who triggered this event. NULL for system events.");
        events.Column("user_name", SqlType.Text).Comment("The display name of the user who triggered this event. NULL for system events.");
        events.PrimaryKey("pk_claim_events", ["aggregate_id", "sequence_number"]);
        events.Index("ix_claim_events_global_sequence", ["global_sequence"]).Unique();

        var integrationEvents = claims.Table("integration_events")
            .Comment("Outbox table for integration events waiting to be published to the message broker.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        integrationEvents.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        integrationEvents.Column("event_type", SqlType.Text).NotNull().Comment("The fully qualified .NET type name of the integration event.");
        integrationEvents.Column("payload", SqlType.Custom("jsonb")).NotNull().Comment("The event payload.");
        integrationEvents.Column("created_at", SqlType.DateTimeOffset).NotNull().Comment("When the event was written to the outbox.");
        integrationEvents.Column("published_at", SqlType.DateTimeOffset).Comment("When the event was published to the message broker. NULL if not yet published.");
        integrationEvents.Column("metadata", SqlType.Custom("jsonb")).NotNull().Default("'{}'::jsonb").Comment("Envelope metadata propagated to consumers via the x-abodio-metadata AMQP header.");
        integrationEvents.PrimaryKey("pk_integration_events", ["id"]);

        var projections = claims.Table("projections")
            .Comment("Stores the checkpoint for the last projected claim.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        projections.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Projection id; defined by each concrete projection handler.");
        projections.Column("last_processed_global_sequence", SqlType.BigInt).NotNull().Default("0").Comment("The sequence number of last projected claim.");
        projections.Column("last_updated", SqlType.DateTimeOffset).NotNull().Comment("When the checkpoint was last updated.");
        projections.PrimaryKey("pk_projections", ["id"]);

        var projectionClaimSummaries = claims.Table("projection_claim_summaries")
            .Comment("Stores a projected view of the most up to date claim dashboard data.")
            .Grant("abodio_api", TablePrivilege.Select | TablePrivilege.Insert | TablePrivilege.Update | TablePrivilege.Delete);
        projectionClaimSummaries.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        projectionClaimSummaries.Column("type_id", SqlType.Custom("typeid")).NotNull().Comment("The id for the claim type.");
        projectionClaimSummaries.Column("type_name", SqlType.Text).NotNull().Comment("The name for the claim type.");
        projectionClaimSummaries.Column("status_id", SqlType.Custom("typeid")).NotNull().Comment("The id for the claim status.");
        projectionClaimSummaries.Column("status_name", SqlType.Text).NotNull().Comment("The name for the claim status.");
        projectionClaimSummaries.PrimaryKey("pk_projection_claim_summaries", ["id"]);
    }
}
