using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace CLARIHR.Infrastructure.Logging;

/// <summary>
/// Provides the central Serilog configuration for the application.
/// </summary>
public static class LoggingConfigurationExtensions
{
    private const string LogDirectory = "logs";
    private const string LogFilePattern = "clarihr-.json";
    private const int RetainedFilesLimit = 30;

    /// <summary>
    /// Builds the Serilog configuration used by the API host.
    /// Adds operational context enrichment and the configured sinks.
    /// </summary>
    public static LoggerConfiguration CreateLoggingConfiguration(
        IWebHostEnvironment hostEnvironment)
    {
        var logLevel = GetLogLevel(hostEnvironment.EnvironmentName);

        return new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", hostEnvironment.EnvironmentName)
            .Enrich.WithProperty("ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id)
            .Enrich.When(
                logEvent => logEvent.Exception != null,
                enricher => enricher.WithProperty("HasException", true))
            .Filter.ByExcluding(logEvent =>
                // Reduce EF Core query noise in development logs.
                hostEnvironment.EnvironmentName == "Development" && 
                logEvent.MessageTemplate.Text.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.File(
                new JsonFormatter(),
                Path.Combine(LogDirectory, LogFilePattern),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFilesLimit,
                fileSizeLimitBytes: 104857600, // 100 MB
                shared: true);
    }

    private static LogEventLevel GetLogLevel(string environmentName)
    {
        return environmentName switch
        {
            "Development" => LogEventLevel.Debug,
            "Staging" => LogEventLevel.Information,
            "Production" => LogEventLevel.Warning,
            _ => LogEventLevel.Information
        };
    }
}
