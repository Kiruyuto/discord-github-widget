namespace GitHubWidgetBot.DTOs.GitHub;

internal sealed record GitHubDeviceAuthorization(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    DateTimeOffset ExpiresAt,
    uint PollIntervalSeconds
);