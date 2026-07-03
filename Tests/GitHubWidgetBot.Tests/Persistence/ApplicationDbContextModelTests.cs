using GitHubWidgetBot.Persistence;
using GitHubWidgetBot.Persistence.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GitHubWidgetBot.Tests.Persistence;

internal sealed class ApplicationDbContextModelTests
{
    [Test]
    public async Task OnModelCreating_DefaultModel_UsesGitHubWidgetSchema()
    {
        await using var dbContext = CreateDbContext();

        var defaultSchema = dbContext.Model.GetDefaultSchema();

        await Assert.That(defaultSchema).IsEqualTo("github_widget");
    }

    [Test]
    public async Task OnModelCreating_SetupSessionEntity_ConfiguresExpectedMetadata()
    {
        await using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(SetupSession));
        await Assert.That(entityType).IsNotNull();

        var discordUserIdIndexFound = false;
        var gitHubExpiresAtUtcIndexFound = false;
        foreach (var index in entityType.GetIndexes())
        {
            var properties = index.Properties;
            await Assert.That(properties.Count).IsEqualTo(1);

            var propertyName = properties[0].Name;
            switch (propertyName)
            {
                case nameof(SetupSession.DiscordUserId):
                    await Assert.That(index.IsUnique).IsTrue();
                    discordUserIdIndexFound = true;
                    break;
                case nameof(SetupSession.GitHubExpiresAtUtc):
                    gitHubExpiresAtUtcIndexFound = true;
                    break;
            }
        }

        var gitHubDeviceCode = entityType.FindProperty(nameof(SetupSession.GitHubDeviceCode));
        await Assert.That(gitHubDeviceCode).IsNotNull();
        await Assert.That(gitHubDeviceCode.GetMaxLength()).IsEqualTo(40);

        await Assert.That(discordUserIdIndexFound).IsTrue();
        await Assert.That(gitHubExpiresAtUtcIndexFound).IsTrue();
    }

    [Test]
    public async Task OnModelCreating_RefreshTargetEntity_ConfiguresExpectedMetadata()
    {
        await using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(RefreshTarget));
        await Assert.That(entityType).IsNotNull();

        var discordUserIdIndexFound = false;
        var lastAttemptUtcIndexFound = false;
        foreach (var index in entityType.GetIndexes())
        {
            var properties = index.Properties;
            await Assert.That(properties.Count).IsEqualTo(1);

            var propertyName = properties[0].Name;
            switch (propertyName)
            {
                case nameof(RefreshTarget.DiscordUserId):
                    await Assert.That(index.IsUnique).IsTrue();
                    discordUserIdIndexFound = true;
                    break;
                case nameof(RefreshTarget.LastAttemptUtc):
                    lastAttemptUtcIndexFound = true;
                    break;
            }
        }

        var gitHubUsername = entityType.FindProperty(nameof(RefreshTarget.GitHubUsername));
        await Assert.That(gitHubUsername).IsNotNull();
        await Assert.That(gitHubUsername.GetMaxLength()).IsEqualTo(39);

        var failureCount = entityType.FindProperty(nameof(RefreshTarget.FailureCount));
        await Assert.That(failureCount).IsNotNull();
        await Assert.That(failureCount.GetDefaultValue()).IsEqualTo(0u);

        await Assert.That(discordUserIdIndexFound).IsTrue();
        await Assert.That(lastAttemptUtcIndexFound).IsTrue();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }
}