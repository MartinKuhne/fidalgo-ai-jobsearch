using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Logging.Configuration;

/// <summary>
/// Configuration contract for logging infrastructure settings.
/// Controls minimum log level, file output, retention, and console output.
/// </summary>
public interface ILoggingConfiguration
{
    /// <summary>Minimum log level to record.</summary>
    LogLevel MinimumLevel { get; }

    /// <summary>File path pattern for log files.</summary>
    string LogFilePath { get; }

    /// <summary>Number of days to retain log files.</summary>
    int RetentionDays { get; }

    /// <summary>Whether to write log entries to the console.</summary>
    bool WriteToConsole { get; }

    /// <summary>Whether to write log entries to files.</summary>
    bool WriteToFile { get; }
}