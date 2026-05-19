using Fidalgo.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace Fidalgo.Agent.Agents;

/// <summary>
/// Handles the config command for setting user search configuration.
/// </summary>
public class ConfigCommandHandler
{
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<ConfigCommandHandler> _logger;

    /// <summary>
    /// Creates a new instance of the ConfigCommandHandler.
    /// </summary>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="logger">The logger.</param>
    public ConfigCommandHandler(ConfigurationService configurationService, ILogger<ConfigCommandHandler> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    /// <summary>
    /// Executes the config command: loads a JSON file and persists the configuration.
    /// </summary>
    /// <param name="configFilePath">Path to the JSON configuration file.</param>
    /// <returns>Zero on success, non-zero on failure.</returns>
    public async Task<int> ExecuteAsync(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            _logger.LogError("Configuration file not found: {Path}", configFilePath);
            return 1;
        }

        try
        {
            var dto = _configurationService.LoadFromFile(configFilePath);
            await _configurationService.SaveToDatabaseAsync(dto);
            _logger.LogInformation("Configuration successfully saved for user {Email}", dto.UserEmail);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration: {Message}", ex.Message);
            return 1;
        }
    }
}
