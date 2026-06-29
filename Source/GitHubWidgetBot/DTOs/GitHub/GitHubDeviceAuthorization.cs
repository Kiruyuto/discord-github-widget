namespace GitHubWidgetBot.DTOs.GitHub;

internal sealed record GitHubDeviceAuthorization(
    string DeviceCode,
    string UserCode,
    string VerificationUrl,
    DateTimeOffset ExpiresAt,
    uint PollIntervalSeconds
);