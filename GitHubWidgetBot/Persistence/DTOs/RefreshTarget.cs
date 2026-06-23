namespace GitHubWidgetBot.Persistence.DTOs;

internal class RefreshTarget : DbEntity
{
    public required ulong DiscordUserId { get; init; }
    public required string GitHubUsername { get; init; }

    public required DateTimeOffset LastUpdateUtc { get; set; }
    public required DateTimeOffset LastAttemptUtc { get; set; }
    public required uint FailureCount { get; set; }
}