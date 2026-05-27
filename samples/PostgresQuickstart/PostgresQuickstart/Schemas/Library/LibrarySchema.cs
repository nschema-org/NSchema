using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Library;

public class LibrarySchema : AbstractSchemaProvider
{
    public LibrarySchema()
    {
        Schema("library", schema => schema
            .Comment("Schema for a public library catalogue, members, and loans.")
            .Authors()
            .Books()
            .Members()
            .Loans()
        );
    }
}