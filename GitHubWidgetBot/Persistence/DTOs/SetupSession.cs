namespace GitHubWidgetBot.Persistence.DTOs;

internal class SetupSession : DbEntity
{
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;

    public required ulong DiscordUserId { get; init; }

    public required string GitHubDeviceCode { get; init; }
    public required uint GitHubPollIntervalSeconds { get; init; }
    public required DateTimeOffset GitHubExpiresAtUtc { get; init; }
}