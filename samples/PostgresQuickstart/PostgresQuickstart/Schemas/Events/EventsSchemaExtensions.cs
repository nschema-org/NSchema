using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Events;

internal static class EventsSchemaExtensions
{
    extension(SchemaBuilder schema)
    {
        public SchemaBuilder Venues() => schema.Table("venues", t => t
            .Comment("Physical locations where events are held.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("venues_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Name of the venue."))
            .Column("city", SqlType.Citext, c => c.NotNull().Comment("City the venue is in."))
            .Column("capacity", SqlType.Int, c => c.NotNull().Default("0").Comment("Maximum seated capacity."))
            .Index("uc_venues_name_city", ["name", "city"], i => i.Unique())
        );

        public SchemaBuilder Events() => schema.Table("events", t => t
            .Comment("Scheduled events open for ticketing.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("events_pkey").Comment("Primary key."))
            .Column("title", SqlType.Citext, c => c.NotNull().Comment("Event title."))
            .Column("description", SqlType.Citext, c => c.Comment("Marketing description of the event."))
            .Column("venue_id", SqlType.Text, c => c.NotNull().Comment("Venue hosting the event."))
            .Column("starts_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("Scheduled start time."))
            .Column("ends_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("Scheduled end time."))
            .Column("is_cancelled", SqlType.Boolean, c => c.NotNull().Default("false").Comment("Whether the event has been cancelled."))
            .ForeignKey("fk_events_venue", ["venue_id"], "events", "venues", ["id"], _ => { })
            .Index("ix_events_starts_at", ["starts_at"], _ => { })
        );

        public SchemaBuilder Attendees() => schema.Table("attendees", t => t
            .Comment("People who have purchased tickets.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("attendees_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Attendee's full name."))
            .Column("email", SqlType.Citext, c => c.NotNull().Comment("Email address for ticket delivery."))
            .Index("uc_attendees_email", ["email"], i => i.Unique())
        );

        public SchemaBuilder Tickets() => schema.Table("tickets", t => t
            .Comment("Individual tickets sold for events.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("tickets_pkey").Comment("Primary key."))
            .Column("event_id", SqlType.Text, c => c.NotNull().Comment("Event the ticket grants entry to."))
            .Column("attendee_id", SqlType.Text, c => c.NotNull().Comment("Attendee who owns the ticket."))
            .Column("seat", SqlType.Text, c => c.Comment("Assigned seat, or null for general admission."))
            .Column("price_cents", SqlType.Int, c => c.NotNull().Comment("Price paid in cents."))
            .Column("purchased_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("When the ticket was purchased."))
            .Column("checked_in_at", SqlType.DateTimeOffset, c => c.Comment("When the attendee was checked in, if at all."))
            .ForeignKey("fk_tickets_event", ["event_id"], "events", "events", ["id"], _ => { })
            .ForeignKey("fk_tickets_attendee", ["attendee_id"], "events", "attendees", ["id"], _ => { })
            .Index("ix_tickets_event_id", ["event_id"], _ => { })
            .Index("uc_tickets_event_seat", ["event_id", "seat"], i => i.Unique().Where("(seat IS NOT NULL)"))
        );
    }
}
