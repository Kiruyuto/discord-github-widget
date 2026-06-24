namespace GitHubWidgetBot.Persistence.DTOs;

internal class RefreshTarget : DbEntity
{
    public required ulong DiscordUserId { get; init; }
    public required string GitHubUsername { get; set; }
    public required bool ExcludeUnknown { get; set; }

    public required DateTimeOffset LastUpdateUtc { get; set; }
    public required DateTimeOffset LastAttemptUtc { get; set; }
    public required uint FailureCount { get; set; }

    public void RecordSuccessfulAttempt(DateTimeOffset utcNow)
    {
        LastUpdateUtc = utcNow;
        LastAttemptUtc = utcNow;
        FailureCount = 0;
    }

    public void RecordFailedAttempt(DateTimeOffset utcNow)
    {
        LastAttemptUtc = utcNow;
        FailureCount++;
    }
}