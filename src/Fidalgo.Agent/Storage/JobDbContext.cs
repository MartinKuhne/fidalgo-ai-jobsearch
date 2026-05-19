using Microsoft.EntityFrameworkCore;
using Fidalgo.Agent.Storage;

namespace Fidalgo.Agent.Storage;

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobEntity> Jobs => Set<JobEntity>();

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
