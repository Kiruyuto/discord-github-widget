using GitHubWidgetBot.Persistence.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GitHubWidgetBot.Persistence.Configurations;

internal sealed class SetupSessionConfiguration : IEntityTypeConfiguration<SetupSession>
{
    public void Configure(EntityTypeBuilder<SetupSession> builder)
    {
        builder.HasKey(static x => x.Id);

        builder.Property(static x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(static x => x.CreatedAtUtc).IsRequired();

        builder.Property(static x => x.DiscordUserId).IsRequired();

        builder.Property(static x => x.GitHubDeviceCode).HasMaxLength(40).IsRequired();
        builder.Property(static x => x.GitHubPollIntervalSeconds).IsRequired();
        builder.Property(static x => x.GitHubExpiresAtUtc).IsRequired();

        builder.HasIndex(static x => new { x.DiscordUserId }).IsUnique();
        builder.HasIndex(static x => x.GitHubExpiresAtUtc);
    }
}