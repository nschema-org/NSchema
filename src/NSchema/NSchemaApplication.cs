using Microsoft.Extensions.Hosting;

namespace NSchema;

/// <summary>
/// The main entry point for an NSchema application.
/// </summary>
public sealed class NSchemaApplication : IHost
{
    private bool _hasRun;
    private readonly IHost _host;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public IServiceProvider Services => _host.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_hasRun)
        {
            throw new InvalidOperationException("The application can only be started once.");
        }
        _hasRun = true;
        return _host.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _host.StopAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _host.Dispose();
    }

    /// <summary>
    /// Creates a new <see cref="NSchemaApplicationBuilder"/> with the specified command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments to add to the builder's configuration.</param>
    /// <returns>A new application builder.</returns>
    public static NSchemaApplicationBuilder CreateBuilder(string[]? args = null) => new(new NSchemaApplicationOptions { Args = args });

    /// <summary>
    /// Creates a new <see cref="NSchemaApplicationBuilder"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the builder.</param>
    /// <returns>A new application builder.</returns>
    public static NSchemaApplicationBuilder CreateBuilder(NSchemaApplicationOptions options) => new(options);
}
