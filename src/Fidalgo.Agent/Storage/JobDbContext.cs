using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.Scraping;
using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Storage;

/// <summary>
/// Entity Framework Core DbContext for the job scraper agent database.
/// </summary>
public class JobDbContext : DbContext
{
    /// <summary>
    /// Creates a new instance of the JobDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public JobDbContext(DbContextOptions<JobDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet of job postings discovered from websites.
    /// </summary>
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    /// <summary>
    /// DbSet of search configurations per user.
    /// </summary>
    public DbSet<SearchConfiguration> SearchConfigurations => Set<SearchConfiguration>();

    /// <summary>
    /// DbSet of scrape result records.
    /// </summary>
    public DbSet<ScrapeResult> ScrapeResults => Set<ScrapeResult>();

    /// <summary>
    /// Configures the database schema.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceUrl).IsUnique();
            entity.Property(e => e.SourceUrl).IsRequired();
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Company).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.MatchedKeywords).IsRequired();
            entity.Property(e => e.SourceWebsite).IsRequired();
            entity.Property(e => e.ScrapedAt).IsRequired();
            entity.Property(e => e.IsDiscarded).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3);
        });

        modelBuilder.Entity<SearchConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserEmail);
            entity.Property(e => e.UserEmail).IsRequired();
            entity.Property(e => e.Websites).IsRequired();
            entity.Property(e => e.Keywords).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ScrapeResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConfigurationId);
            entity.Property(e => e.ConfigurationId).IsRequired();
            entity.Property(e => e.Website).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.JobsFound).IsRequired();
            entity.Property(e => e.JobsSkipped).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();
        });
    }
}
