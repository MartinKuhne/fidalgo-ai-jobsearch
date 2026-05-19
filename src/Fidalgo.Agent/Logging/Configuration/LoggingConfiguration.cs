using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Logging.Configuration;

public class LoggingConfiguration : ILoggingConfiguration
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public string LogFilePath { get; set; } = "logs/log-.json";
    public int RetentionDays { get; set; } = 7;
    public bool WriteToConsole { get; set; } = true;
    public bool WriteToFile { get; set; } = true;
}
