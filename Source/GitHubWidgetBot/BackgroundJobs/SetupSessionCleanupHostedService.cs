using GitHubWidgetBot.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitHubWidgetBot.BackgroundJobs;

internal sealed class SetupSessionCleanupHostedService(ILogger<SetupSessionCleanupHostedService> logger, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(period: RunInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredSetupSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Unhandled failure while deleting expired setup sessions");
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

    private async Task DeleteExpiredSetupSessionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deletedCount = await dbContext.SetupSessions
            .Where(session => session.GitHubExpiresAtUtc <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount != 0 && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Deleted {DeletedSetupSessionCount} expired setup session(s)", deletedCount);
    }
}