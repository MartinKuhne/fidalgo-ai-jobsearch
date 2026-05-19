using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Logging.Configuration;

public interface ILoggingConfiguration
{
    LogLevel MinimumLevel { get; }
    string LogFilePath { get; }
    int RetentionDays { get; }
    bool WriteToConsole { get; }
    bool WriteToFile { get; }
}
