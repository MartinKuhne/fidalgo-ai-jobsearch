using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Logging.Configuration;

/// <summary>
/// Default logging configuration with sensible defaults for agent applications.
/// Information level, JSON file output, 7-day retention, console and file enabled.
/// </summary>
public class LoggingConfiguration : ILoggingConfiguration
{
    /// <summary>Minimum log level to record (default: Information).</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>File path pattern for log files (default: logs/log-.json).</summary>
    public string LogFilePath { get; set; } = "logs/log-.json";

    /// <summary>Number of days to retain log files (default: 7).</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Whether to write log entries to the console (default: true).</summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>Whether to write log entries to files (default: true).</summary>
    public bool WriteToFile { get; set; } = true;
}