using Microsoft.Extensions.Logging;
using NuGet.Common;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NuGet.Common.LogLevel;

namespace NSchema.Build.Helpers;

#pragma warning disable CA2254
public class NuGetLoggerAdapter(ILogger logger) : NuGet.Common.ILogger
{
    public void LogDebug(string data) => logger.LogDebug(data);
    public void LogVerbose(string data) => logger.LogTrace(data);
    public void LogInformation(string data) => logger.LogInformation(data);
    public void LogMinimal(string data) => logger.LogInformation(data);
    public void LogWarning(string data) => logger.LogWarning(data);
    public void LogError(string data) => logger.LogWarning(data);
    public void LogInformationSummary(string data) => logger.LogInformation(data);

    public void Log(LogLevel level, string data) => logger.Log(level switch
    {
        LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
        LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
        LogLevel.Minimal => Microsoft.Extensions.Logging.LogLevel.Information,
        LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    }, data);
    public Task LogAsync(LogLevel level, string data) { Log(level, data); return Task.CompletedTask; }
    public void Log(ILogMessage message) => Log(message.Level, message.Message);
    public Task LogAsync(ILogMessage message) { Log(message); return Task.CompletedTask; }
}

