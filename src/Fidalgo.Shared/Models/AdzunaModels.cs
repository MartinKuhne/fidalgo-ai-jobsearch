using System.Text.Json.Serialization;

namespace Fidalgo.Shared.Models;

/// <summary>
/// Represents a job result from the Adzuna API.
/// </summary>
public class AdzunaJob
{
    /// <summary>The unique job identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The job title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>The job description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>The employer/company name.</summary>
    [JsonPropertyName("company")]
    public AdzunaCompany? Company { get; set; }

    /// <summary>The job location.</summary>
    [JsonPropertyName("location")]
    public AdzunaLocation? Location { get; set; }

    /// <summary>The minimum salary, or null if not specified.</summary>
    [JsonPropertyName("salary_min")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SalaryMin { get; set; }

    /// <summary>The maximum salary, or null if not specified.</summary>
    [JsonPropertyName("salary_max")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SalaryMax { get; set; }

    /// <summary>Whether the salary is a predicted value.</summary>
    [JsonPropertyName("salary_is_predicted")]
    public string? SalaryIsPredicted { get; set; }

    /// <summary>The type of contract (e.g., permanent, contract).</summary>
    [JsonPropertyName("contract_type")]
    public string? ContractType { get; set; }

    /// <summary>The type of time (e.g., full_time, part_time).</summary>
    [JsonPropertyName("contract_time")]
    public string? ContractTime { get; set; }

    /// <summary>The category the job belongs to.</summary>
    [JsonPropertyName("category")]
    public AdzunaCategory? Category { get; set; }

    /// <summary>The URL to redirect to for the full job listing.</summary>
    [JsonPropertyName("redirect_url")]
    public string? RedirectUrl { get; set; }

    /// <summary>The date the job was created.</summary>
    [JsonPropertyName("created")]
    public string? Created { get; set; }

    /// <summary>The latitude of the job location.</summary>
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    /// <summary>The longitude of the job location.</summary>
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

/// <summary>
/// Represents a location from the Adzuna API.
/// </summary>
public class AdzunaLocation
{
    /// <summary>The display name of the location.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>The hierarchical area array.</summary>
    [JsonPropertyName("area")]
    public List<string>? Area { get; set; }
}

/// <summary>
/// Represents a company from the Adzuna API.
/// </summary>
public class AdzunaCompany
{
    /// <summary>The display name of the company.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// Represents a category from the Adzuna API.
/// </summary>
public class AdzunaCategory
{
    /// <summary>The human-readable label.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>The category tag.</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
}

/// <summary>
/// Represents the search results response from the Adzuna API.
/// </summary>
public class AdzunaSearchResponse
{
    /// <summary>The list of job results.</summary>
    [JsonPropertyName("results")]
    public List<AdzunaJob> Results { get; set; } = new();

    /// <summary>The total number of results available.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>The starting index of the results.</summary>
    [JsonPropertyName("from")]
    public int From { get; set; }

    /// <summary>The number of results returned.</summary>
    [JsonPropertyName("to")]
    public int To { get; set; }

    /// <summary>The total number of results matching the search.</summary>
    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    /// <summary>The number of results per page.</summary>
    [JsonPropertyName("results_per_page")]
    public int ResultsPerPage { get; set; }
}