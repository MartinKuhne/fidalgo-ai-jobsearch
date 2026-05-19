using System.Text.Json;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Configuration;

/// <summary>
/// Service for managing search configuration.
/// </summary>
public class ConfigurationService
{
    private readonly ConfigRepository _repository;
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Creates a new instance of the ConfigurationService.
    /// </summary>
    /// <param name="repository">The configuration repository.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationService(ConfigRepository repository, ILogger<ConfigurationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Loads configuration from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON configuration file.</param>
    /// <returns>The deserialized configuration DTO.</returns>
    public SearchConfigDto LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<SearchConfigDto>(json)
            ?? throw new InvalidOperationException("Configuration file is empty or invalid.");
    }

    /// <summary>
    /// Saves a configuration to the database.
    /// </summary>
    /// <param name="dto">The configuration DTO to save.</param>
    public async Task SaveToDatabaseAsync(SearchConfigDto dto)
    {
        ValidateWebsites(dto.Websites);
        ValidateKeywords(dto.Keywords);

        var config = new SearchConfiguration
        {
            UserEmail = dto.UserEmail,
            Websites = JsonSerializer.Serialize(dto.Websites),
            Keywords = JsonSerializer.Serialize(dto.Keywords),
            IsActive = true
        };

        await _repository.UpsertConfigAsync(config);
        _logger.LogInformation("Configuration saved for user {Email}", dto.UserEmail);
    }

    /// <summary>
    /// Retrieves the active configuration for a user.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>The configuration DTO, or null if none exists.</returns>
    public async Task<SearchConfigDto?> GetConfigAsync(string email)
    {
        var config = await _repository.GetActiveConfigAsync(email);
        if (config == null)
            return null;

        return new SearchConfigDto
        {
            UserEmail = config.UserEmail,
            Websites = JsonSerializer.Deserialize<List<string>>(config.Websites) ?? new(),
            Keywords = JsonSerializer.Deserialize<List<string>>(config.Keywords) ?? new()
        };
    }

    /// <summary>
    /// Validates that all websites in the list are supported.
    /// </summary>
    /// <param name="websites">The list of websites to validate.</param>
    /// <exception cref="ArgumentException">Thrown when an unsupported website is found.</exception>
    public void ValidateWebsites(IEnumerable<string> websites)
    {
        foreach (var website in websites)
        {
            if (!SupportedWebsites.IsSupported(website))
            {
                throw new ArgumentException(
                    $"Unsupported website: '{website}'. Supported: {string.Join(", ", SupportedWebsites.All)}");
            }
        }
    }

    /// <summary>
    /// Validates that the keyword list is non-empty.
    /// </summary>
    /// <param name="keywords">The list of keywords to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the list is empty.</exception>
    public void ValidateKeywords(IEnumerable<string> keywords)
    {
        var keywordList = keywords.ToList();
        if (!keywordList.Any())
        {
            throw new ArgumentException("At least one search keyword is required.");
        }
    }
}
