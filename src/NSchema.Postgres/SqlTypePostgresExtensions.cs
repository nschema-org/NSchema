using NSchema.Schema;

namespace NSchema.Postgres;

/// <summary>
/// Provides extension methods for defining PostgreSQL-specific SQL types in NSchema.
/// </summary>
public static class SqlTypePostgresExtensions
{
    extension(SqlType)
    {
        /// <summary>
        /// Represents the PostgreSQL "citext" type, which is a case-insensitive text type.
        /// </summary>
        public static SqlType Citext => SqlType.Custom("citext");

        /// <summary>
        /// Represents the PostgreSQL "jsonb" type, which is a binary JSON type that allows for efficient storage and querying of JSON data.
        /// </summary>
        public static SqlType Jsonb => SqlType.Custom("jsonb");
    }
}
