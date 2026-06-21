using NSchema.Configuration.Ddl;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the SQLite database provider.
/// </summary>
internal sealed class SqliteProviderConfig
{
    /// <summary>
    /// The connection string used to reach the database, e.g. <c>Data Source=app.db</c>.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Maps a <c>PROVIDER sqlite</c> block's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static SqliteProviderConfig FromBlock(ConfigBlock block)
    {
        var config = new SqliteProviderConfig();
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "connection_string":
                    config.ConnectionString = value.AsString();
                    break;
                default:
                    throw block.UnknownAttribute(key);
            }
        }

        return config;
    }
}
