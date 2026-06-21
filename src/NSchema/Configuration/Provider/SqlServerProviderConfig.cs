using NSchema.Configuration.Ddl;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the SQL Server database provider.
/// </summary>
internal sealed class SqlServerProviderConfig
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
    /// The command timeout, in seconds. When null, the SqlClient default is used.
    /// </summary>
    public int? CommandTimeout { get; set; }

    /// <summary>
    /// Maps a <c>PROVIDER sqlserver</c> block's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static SqlServerProviderConfig FromBlock(ConfigBlock block)
    {
        var config = new SqlServerProviderConfig();
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
