using GitHubWidgetBot.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace GitHubWidgetBot.Services;

internal sealed class WidgetRefreshHostedService(ILogger<WidgetRefreshHostedService> logger, IServiceScopeFactory serviceScopeFactory, GitHubService gitHubService, RestClient restClient) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);

    private const uint MaxFailureCount = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var applicationId = 0UL;
        using var timer = new PeriodicTimer(period: RunInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (applicationId == 0) applicationId = (await restClient.GetCurrentApplicationAsync(cancellationToken: stoppingToken)).Id;

                await RefreshDueTargetsAsync(applicationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Unhandled failure while preparing or refreshing GitHub widget targets");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshDueTargetsAsync(ulong applicationId, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var dueBeforeUtc = DateTimeOffset.UtcNow.Subtract(RefreshInterval);
        var refreshTargets = await dbContext.RefreshTargets
            .Where(static target => target.FailureCount < MaxFailureCount)
            .Where(target => target.LastAttemptUtc <= dueBeforeUtc)
            .OrderBy(static target => target.LastAttemptUtc)
            .ToArrayAsync(cancellationToken);

        if (refreshTargets.Length == 0)
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("No GitHub widget refresh targets are due");
            return;
        }

        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Refreshing {RefreshTargetCount} GitHub widget target(s)", refreshTargets.Length);

        foreach (var refreshTarget in refreshTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var failed = true;
            var attemptUtc = DateTimeOffset.UtcNow;
            var widget = await gitHubService.FetchUserDataAsync(refreshTarget.GitHubUsername, refreshTarget.ExcludeUnknown);

            if (widget.HasValue)
            {
                try
                {
                    using var content = widget.Value.ToJsonContent();
                    await restClient.SendRequestAsync(
                        method: HttpMethod.Patch,
                        content: content,
                        route: $"/applications/{applicationId}/users/{refreshTarget.DiscordUserId}/identities/{refreshTarget.DiscordUserId}/profile",
                        cancellationToken: cancellationToken
                    );

                    refreshTarget.RecordSuccessfulAttempt(attemptUtc);
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Refreshed widget for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}",
                            refreshTarget.DiscordUserId, refreshTarget.GitHubUsername
                        );
                    }

                    failed = false;
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to update Discord widget profile for Discord user {DiscordUserId}", refreshTarget.DiscordUserId);
                }
            }

            if (failed)
            {
                refreshTarget.RecordFailedAttempt(attemptUtc);
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Failed to refresh widget for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}. Failure count: {FailureCount}",
                        refreshTarget.DiscordUserId, refreshTarget.GitHubUsername, refreshTarget.FailureCount
                    );
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}