using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Events;

public class EventsSchema : AbstractSchemaProvider
{
    public EventsSchema()
    {
        Schema("events", schema => schema
            .Comment("Schema for event ticketing — venues, events, and tickets.")
            .Venues()
            .Events()
            .Attendees()
            .Tickets()
        );
    }
}
