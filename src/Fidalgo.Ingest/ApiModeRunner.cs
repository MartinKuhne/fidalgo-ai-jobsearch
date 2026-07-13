using Fidalgo.Ingest.Configuration;
using Fidalgo.Ingest.Tools;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Fidalgo.Ingest;

public class ApiModeRunner
{
    private readonly JobSearchTool _jobSearchTool;
    private readonly JobInDbTool _jobInDbTool;
    private readonly SaveJobTool _saveJobTool;
    private readonly ILogger<ApiModeRunner> _logger;

    public ApiModeRunner(
        JobSearchTool jobSearchTool,
        JobInDbTool jobInDbTool,
        SaveJobTool saveJobTool,
        ILogger<ApiModeRunner> logger)
    {
        _jobSearchTool = jobSearchTool;
        _jobInDbTool = jobInDbTool;
        _saveJobTool = saveJobTool;
        _logger = logger;
    }

    public async Task RunAsync(CliOptions options)
    {
        int page = 1;
        while (true)
        {
            if (page > 1)
            {
                await Task.Delay(1000);
            }
            var jobs = await _jobSearchTool.SearchRawAsync(options.Keywords, options.ZipCode, page: page);
            
            if (jobs == null || jobs.Results.Count == 0)
            {
                break;
            }

            _logger.LogInformation("API search found {ResultCount} jobs on page {Page} (Total available: {Count}).", jobs.Results.Count, page, jobs.Count);
            
            foreach (var job in jobs.Results)
            {
                var exists = await _jobInDbTool.ContainsAsync(options.Email, "adzuna", job.Id ?? string.Empty);
                if (!exists && !string.IsNullOrEmpty(job.RedirectUrl))
                {
                    _logger.LogInformation("Saving job {JobId} from {Url}", job.Id, job.RedirectUrl);
                    DateTime.TryParse(job.Created, out var postedDate);

                    var jobId = await _saveJobTool.SaveAsync(
                        options.Email,
                        job.Company?.DisplayName ?? "Unknown",
                        job.Title,
                        job.Id,
                        postedDate == default ? null : postedDate,
                        job.SalaryMin,
                        job.SalaryMax,
                        job.RedirectUrl,
                        "adzuna"
                    );

                    await Task.Delay(1000);
                }
                else if (exists)
                {
                    _logger.LogInformation("Skipping job {JobId} because it already exists in the database.", job.Id);
                }
            }

            if (jobs.Results.Count < 20)
            {
                break;
            }
            page++;
        }
    }
}
