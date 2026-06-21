using NSchema.Configuration.Ddl;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the PostgreSQL database provider.
/// </summary>
internal sealed class PostgresProviderConfig
{
    /// <summary>
    /// The connection string used to reach the database.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// The username, supplied separately from the connection string.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The password, supplied separately from the connection string.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The command timeout, in seconds. When null, Npgsql's default is used.
    /// </summary>
    public int? CommandTimeout { get; set; }

    /// <summary>
    /// Maps a <c>PROVIDER postgres</c> block's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static PostgresProviderConfig FromBlock(ConfigBlock block)
    {
        var config = new PostgresProviderConfig();
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "connection_string":
                    config.ConnectionString = value.AsString();
                    break;
                case "username":
                    config.Username = value.AsString();
                    break;
                case "password":
                    config.Password = value.AsString();
                    break;
                case "command_timeout":
                    config.CommandTimeout = (int)value.AsInteger();
                    break;
                default:
                    throw block.UnknownAttribute(key);
            }
        }

        return config;
    }
}
