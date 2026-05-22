using NSchema.Schema;

namespace NSchema.Sandbox;

internal static class SqlTypeExtensions
{
    extension(SqlType)
    {
        public static SqlType TypeId => SqlType.Custom("typeid");
    }
}
