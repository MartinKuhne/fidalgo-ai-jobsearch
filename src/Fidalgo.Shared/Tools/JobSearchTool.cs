using System.Text;
using System.Text.Json;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.Tools;

/// <summary>
/// Agent tool to search for jobs using the Adzuna job search API.
/// Allows searching by keywords, location, and various filters.
/// </summary>
public class JobSearchTool
{
    private readonly HttpClient _httpClient;
    private readonly AdzunaConfiguration _config;
    private readonly ILogger<JobSearchTool> _logger;

    private const string BaseUrl = "https://api.adzuna.com/v1/api/jobs";

    /// <summary>Initializes a new instance of the JobSearchTool.</summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="config">Adzuna API configuration.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public JobSearchTool(HttpClient httpClient, IOptions<AdzunaConfiguration> config, ILogger<JobSearchTool> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Searches for jobs using the Adzuna API.
    /// </summary>
    /// <param name="keywords">Job title or keywords to search for (e.g., "software engineer").</param>
    /// <param name="location">Location to search in (e.g., "london", "new york").</param>
    /// <param name="country">Country code (default: "us"). Supported codes: us, gb, de, fr, au, ca.</param>
    /// <param name="resultsPerPage">Number of results to return (1-100, default: 20).</param>
    /// <param name="page">Page number for pagination (default: 1).</param>
    /// <param name="salaryMin">Minimum salary in currency units (optional).</param>
    /// <param name="contractType">Filter by contract type: permanent, contract, internship, trainee (optional).</param>
    /// <param name="contractTime">Filter by contract time: full_time, part_time (optional).</param>
    /// <param name="includeSeniority">Whether to include seniority level in results (default: false).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>Formatted string of job search results.</returns>
    public async Task<string> SearchAsync(
        string keywords,
        string location,
        string country = "us",
        int resultsPerPage = 20,
        int page = 1,
        decimal? salaryMin = null,
        string? contractType = null,
        string? contractTime = null,
        bool includeSeniority = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Searching Adzuna jobs: keywords={Keywords} location={Location} country={Country} page={Page}",
            keywords, location, country, page);

        var url = BuildUrl(keywords, location, country, resultsPerPage, page, salaryMin, contractType, contractTime, includeSeniority);

        var searchResponse = await SearchRawAsync(keywords, location, country, resultsPerPage, page, salaryMin, contractType, contractTime, includeSeniority, cancellationToken);
        if (searchResponse == null || searchResponse.Results.Count == 0)
        {
            return $"No jobs found for '{keywords}' in {location}.";
        }
        return FormatResults(searchResponse, keywords, location);
    }

    public async Task<AdzunaSearchResponse?> SearchRawAsync(
        string keywords,
        string location,
        string country = "us",
        int resultsPerPage = 20,
        int page = 1,
        decimal? salaryMin = null,
        string? contractType = null,
        string? contractTime = null,
        bool includeSeniority = false,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(keywords, location, country, resultsPerPage, page, salaryMin, contractType, contractTime, includeSeniority);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var errorContent = Encoding.UTF8.GetString(errorBytes);
                _logger.LogError(
                    "Adzuna API returned status {StatusCode}: {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var searchResponse = await JsonSerializer.DeserializeAsync<AdzunaSearchResponse>(
                stream, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, 
                cancellationToken);

            if (searchResponse == null || searchResponse.Results.Count == 0)
            {
                _logger.LogInformation("No jobs found for keywords='{Keywords}' in {Location}", keywords, location);
                return null;
            }

            _logger.LogInformation(
                "Found {ResultCount} jobs for keywords='{Keywords}' in {Location}",
                searchResponse.Results.Count, keywords, location);

            return searchResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Adzuna jobs for keywords='{Keywords}' in {Location}", keywords, location);
            return null;
        }
    }

    private string BuildUrl(
        string keywords,
        string location,
        string country,
        int resultsPerPage,
        int page,
        decimal? salaryMin,
        string? contractType,
        string? contractTime,
        bool includeSeniority)
    {
        var queryParts = new List<string>
        {
            $"app_id={Uri.EscapeDataString(_config.AppId)}",
            $"app_key={Uri.EscapeDataString(_config.AppKey)}",
            $"what={Uri.EscapeDataString(keywords)}",
            $"where={Uri.EscapeDataString(location)}",
            $"results_per_page={Math.Clamp(resultsPerPage, 1, 100)}"
        };

        if (salaryMin.HasValue && salaryMin.Value > 0)
        {
            queryParts.Add($"salary_min={salaryMin.Value}");
        }

        if (!string.IsNullOrEmpty(contractType))
        {
            if (contractType.Contains("contract", StringComparison.OrdinalIgnoreCase))
                queryParts.Add("contract=1");
            else if (contractType.Contains("permanent", StringComparison.OrdinalIgnoreCase))
                queryParts.Add("permanent=1");
        }

        if (!string.IsNullOrEmpty(contractTime))
        {
            var cTime = contractTime.ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            if (cTime.Contains("full_time") || cTime.Contains("fulltime"))
                queryParts.Add("full_time=1");
            else if (cTime.Contains("part_time") || cTime.Contains("parttime"))
                queryParts.Add("part_time=1");
        }

        var queryString = string.Join("&", queryParts);
        return $"{BaseUrl}/{country}/search/{page}?{queryString}";
    }

    private string FormatResults(AdzunaSearchResponse response, string keywords, string location)
    {
        var output = new StringBuilder();
        output.AppendLine($"Found {response.TotalResults} jobs for '{keywords}' in {location} (showing {response.Results.Count} of {response.Count} results on this page):");
        output.AppendLine();

        foreach (var job in response.Results)
        {
            output.AppendLine($"## {job.Title ?? "Untitled"}");
            output.AppendLine($"ID: {job.Id}");

            if (job.Company?.DisplayName != null)
            {
                output.AppendLine($"Company: {job.Company.DisplayName}");
            }

            if (job.Location?.DisplayName != null)
            {
                output.AppendLine($"Location: {job.Location.DisplayName}");
            }

            if (job.SalaryMin.HasValue || job.SalaryMax.HasValue)
            {
                var min = job.SalaryMin ?? 0m;
                var max = job.SalaryMax;
                if (max.HasValue && max.Value > min)
                {
                    output.AppendLine($"Salary: {FormatSalary(min)} - {FormatSalary(max.Value)}");
                }
                else
                {
                    output.AppendLine($"Salary: {FormatSalary(min)}");
                }
            }
            else
            {
                output.AppendLine("Salary: Not specified");
            }

            if (job.ContractType != null)
            {
                output.AppendLine($"Contract: {job.ContractType}{(job.ContractTime != null ? $" ({job.ContractTime})" : "")}");
            }

            if (job.Category?.Label != null)
            {
                output.AppendLine($"Category: {job.Category.Label}");
            }

            if (job.Created != null)
            {
                output.AppendLine($"Posted: {job.Created}");
            }

            if (job.RedirectUrl != null)
            {
                output.AppendLine($"URL: {job.RedirectUrl}");
            }

            if (job.Description != null)
            {
                var truncated = job.Description.Length > 500
                    ? job.Description[..497] + "..."
                    : job.Description;
                output.AppendLine(truncated.Replace("\n", " ").Replace("\r", " "));
            }

            output.AppendLine();
        }

        return output.ToString();
    }

    private string FormatSalary(decimal salary)
    {
        return salary.ToString("C0", System.Globalization.CultureInfo.InvariantCulture);
    }
}