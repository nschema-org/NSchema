using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClientsSchema : AbstractSchemaProvider
{
    public ClientsSchema()
    {
        var clients = Schema("clients")
            .Comment("Schema for client and policy management.")
            .Grant(Roles.Api);

        AddIntegrationEventsTable(clients);
        AddEventsTable(clients);
        AddProjectionsTable(clients);
        AddInsurers(clients);
        AddPolicyTypes(clients);
        AddPoliciesProjection(clients);
        AddLossAdjusters(clients);
        AddInsurerReferences(clients);
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
        table.Column("metadata", SqlType.Jsonb).NotNull().Default("'{}'::jsonb").Comment("Envelope metadata (originator, and any future fields like event version or profile id). Propagated to consumers via the x-abodio-metadata AMQP header.");
    }

    private static void AddEventsTable(SchemaBuilder schema)
    {
        var events = schema.Table("events")
            .Comment("Append-only event store.")
            .Grant(Roles.Api, TablePrivilege.AppendOnly);

        events.Column("aggregate_id", SqlType.Text).NotNull().Comment("The claim ID this event belongs to.");
        events.Column("sequence_number", SqlType.Int).NotNull().Comment("Ordering of events within a single claim.");
        events.Column("global_sequence", SqlType.BigInt).NotNull().Identity(startWith: 0, minValue: 0).Comment("Monotonically increasing sequence across all aggregates. Used by projections for checkpointing.");
        events.Column("timestamp", SqlType.DateTimeOffset).NotNull().Comment("UTC timestamp when the event was recorded.");
        events.Column("event_type", SqlType.Text).NotNull().Comment("Discriminator used for deserialization.");
        events.Column("payload", SqlType.Jsonb).NotNull().Comment("JSON-serialized event data.");
        events.Column("user_id", SqlType.Text).Comment("The ID of the user who triggered this event. NULL for system events.");
        events.Column("user_name", SqlType.Text).Comment("The display name of the user who triggered this event. NULL for system events.");
        events.PrimaryKey("pk_client_events", ["aggregate_id", "sequence_number"]);
        events.Index("ix_client_events_global_sequence", ["global_sequence"]).Unique();
    }

    private static void AddProjectionsTable(SchemaBuilder schema)
    {
        var table = schema.Table("projections")
            .Comment("Stores information about event sourcing projections.")
            .Grant(Roles.Api, TablePrivilege.All);

        table.Column("id", SqlType.Text).Comment("Unique identifier for the projection.").PrimaryKey("projections_pkey");
        table.Column("last_processed_global_sequence", SqlType.BigInt).NotNull().Default("0").Comment("The global sequence number of the last event applied to the projection.");
        table.Column("last_updated", SqlType.DateTimeOffset).NotNull().Comment("When the checkpoint was last updated");
    }

    private static void AddInsurers(SchemaBuilder schema)
    {
        var table = schema.Table("insurers")
            .Comment("Stores information about all the insurance companies we work for.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("insurers_pkey");
        table.Column("name", SqlType.Text).NotNull().Comment("Business name of the insurer.");
        table.Index("uc_clients_name", ["name"]).Unique();
    }

    private static void AddPolicyTypes(SchemaBuilder schema)
    {
        var table = schema.Table("policy_types")
            .Comment("Stores types of insurance policies that Abodio manages.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("policy_types_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the policy type. Must be unique.");
        table.Column("description", SqlType.Citext).NotNull().Default("''::citext").Comment("Description of the policy type.");
        table.Column("is_domestic", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers domestic properties.");
        table.Column("is_commercial", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers commercial properties.");
        table.Index("uc_policy_types_name", ["name"]).Unique();
    }

    private static void AddPoliciesProjection(SchemaBuilder schema)
    {
        var table = schema.Table("projection_policies")
            .Comment("Stores a summary of policies that belong to insurers.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key of the insurance policy.").PrimaryKey("projection_policies_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the policy.");
        table.Column("insurer_id", SqlType.Text).NotNull().Comment("The insurer to whom this policy belongs.");
        table.Column("insurer_name", SqlType.Text).NotNull().Comment("The name of the insurer to whom this policy belongs.");
        table.Column("policy_type_id", SqlType.Text).NotNull().Comment("The type of policy.");
        table.Column("policy_type_name", SqlType.Text).NotNull().Comment("The name of the type of policy.");
    }

    private static void AddLossAdjusters(SchemaBuilder schema)
    {
        var table = schema.Table("loss_adjusters")
            .Comment("Stores information about all the loss adjusters we work with.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("loss_adjusters_pkey");
        table.Column("name", SqlType.Citext).NotNull().Comment("Business name of the loss adjuster.");
        table.Index("uc_loss_adjusters_name", ["name"]).Unique();
    }

    private static void AddInsurerReferences(SchemaBuilder schema)
    {
        var table = schema.Table("insurer_references")
            .Comment("Describes reference numbers that must be given when a claim is logged for the given insurer.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.Text).Comment("Primary key.").PrimaryKey("insurer_references_pkey");
        table.Column("insurer_id", SqlType.Text).NotNull().Comment("Foreign key referencing the insurer for whom this reference number needs to be taken.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the reference (e.g. \"Aviva Reference\" or \"NFUM Number\").");
        table.Column("input_mask", SqlType.Text).NotNull().Comment("A pattern that the reference number must match. See domain model for details.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the reference number and what it is used for.");
        table.ForeignKey("fk_insurer_references_insurer", ["insurer_id"], "clients", "insurers", ["id"]);
    }
}
