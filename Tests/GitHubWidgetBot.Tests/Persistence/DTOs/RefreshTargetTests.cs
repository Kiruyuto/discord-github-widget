using GitHubWidgetBot.Persistence.DTOs;

namespace GitHubWidgetBot.Tests.Persistence.DTOs;

internal sealed class RefreshTargetTests
{
    [Test]
    public async Task RecordSuccessfulAttempt_PreviousFailureState_UpdatesAttemptAndResetsFailureCount()
    {
        var previousAttemptUtc = new DateTimeOffset(2026, 6, 24, 8, 0, 0, TimeSpan.Zero);
        var previousUpdateUtc = new DateTimeOffset(2026, 6, 24, 7, 0, 0, TimeSpan.Zero);
        var attemptUtc = new DateTimeOffset(2026, 6, 24, 14, 0, 0, TimeSpan.Zero);
        var refreshTarget = new RefreshTarget
        {
            DiscordUserId = 123,
            GitHubUsername = "Kiruyuto",
            ExcludeUnknown = true,
            LastAttemptUtc = previousAttemptUtc,
            LastUpdateUtc = previousUpdateUtc,
            FailureCount = 4
        };

        refreshTarget.RecordSuccessfulAttempt(attemptUtc);

        await Assert.That(refreshTarget.LastAttemptUtc).IsEqualTo(attemptUtc);
        await Assert.That(refreshTarget.LastUpdateUtc).IsEqualTo(attemptUtc);
        await Assert.That(refreshTarget.FailureCount).IsEqualTo(0u);
    }

    [Test]
    public async Task RecordFailedAttempt_PreviousFailureState_UpdatesAttemptAndIncrementsFailureCount()
    {
        var previousAttemptUtc = new DateTimeOffset(2026, 6, 24, 8, 0, 0, TimeSpan.Zero);
        var previousUpdateUtc = new DateTimeOffset(2026, 6, 24, 7, 0, 0, TimeSpan.Zero);
        var attemptUtc = new DateTimeOffset(2026, 6, 24, 14, 0, 0, TimeSpan.Zero);
        var refreshTarget = new RefreshTarget
        {
            DiscordUserId = 123,
            GitHubUsername = "Kiruyuto",
            ExcludeUnknown = true,
            LastAttemptUtc = previousAttemptUtc,
            LastUpdateUtc = previousUpdateUtc,
            FailureCount = 4
        };

        refreshTarget.RecordFailedAttempt(attemptUtc);

        await Assert.That(refreshTarget.LastAttemptUtc).IsEqualTo(attemptUtc);
        await Assert.That(refreshTarget.LastUpdateUtc).IsEqualTo(previousUpdateUtc);
        await Assert.That(refreshTarget.FailureCount).IsEqualTo(5u);
    }
}