using GitHubWidgetBot.Persistence.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GitHubWidgetBot.Persistence.Configurations;

internal sealed class RefreshTargetConfiguration : IEntityTypeConfiguration<RefreshTarget>
{
    public void Configure(EntityTypeBuilder<RefreshTarget> builder)
    {
        builder.HasKey(static x => x.Id);

        builder.Property(static x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(static x => x.DiscordUserId).IsRequired();
        builder.Property(static x => x.GitHubUsername).IsRequired().HasMaxLength(39); // 39 is the upper bound GitHub constraint
        builder.Property(static x => x.ExcludeUnknown).IsRequired();
        builder.Property(static x => x.LastUpdateUtc).IsRequired();
        builder.Property(static x => x.LastAttemptUtc).IsRequired();
        builder.Property(static x => x.FailureCount).IsRequired().HasDefaultValue(default(uint));

        builder.HasIndex(static x => new { x.DiscordUserId }).IsUnique();
        builder.HasIndex(static x => new { x.LastAttemptUtc });
    }
}