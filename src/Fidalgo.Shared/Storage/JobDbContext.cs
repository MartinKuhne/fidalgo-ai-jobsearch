using Microsoft.EntityFrameworkCore;

namespace Fidalgo.Shared.Storage;

/// <summary>
/// EF Core DbContext for persisting job entities to a SQLite database.
/// Configures the Jobs table schema with keys, indexes, and required property constraints.
/// Shared between Fidalgo.Agent and Fidalgo.Web for common persistence.
/// </summary>
public class JobDbContext : DbContext
{
    /// <summary>Initializes a new instance of the JobDbContext.</summary>
    /// <param name="options">Database configuration options.</param>
    public JobDbContext(DbContextOptions<JobDbContext> options)
        : base(options)
    {
    }

    /// <summary>Set of job entities for CRUD operations.</summary>
    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    /// <summary>Configures the Jobs entity schema with keys, indexes, and constraints.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(e => e.InternalId);
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => new { e.Email, e.EmployerJobId }).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Employer).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Pros).IsRequired();
            entity.Property(e => e.Cons).IsRequired();
            entity.Property(e => e.ResumeHints).IsRequired();
            entity.Property(e => e.Score).IsRequired();
            entity.Property(e => e.Recommendation).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.SourceWebsite).IsRequired();
        });
    }
}