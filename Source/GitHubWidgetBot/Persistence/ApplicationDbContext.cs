using GitHubWidgetBot.Persistence.Configurations;
using GitHubWidgetBot.Persistence.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GitHubWidgetBot.Persistence;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(ApplicationConfiguration.Database.SchemaName);
        modelBuilder.ApplyConfiguration(new RefreshTargetConfiguration());
        modelBuilder.ApplyConfiguration(new SetupSessionConfiguration());
    }

    public DbSet<SetupSession> SetupSessions => Set<SetupSession>();
    public DbSet<RefreshTarget> RefreshTargets => Set<RefreshTarget>();
}