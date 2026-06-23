using Microsoft.Extensions.Options;

namespace GitHubWidgetBot.Services;

internal sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string Token { get; init; } = string.Empty;

    public string OAuthClientId { get; init; } = string.Empty;
}

internal sealed class GitHubOptionsValidator : IValidateOptions<GitHubOptions>
{
    public ValidateOptionsResult Validate(string? name, GitHubOptions options)
    {
        var tokenMissing = string.IsNullOrWhiteSpace(options.Token);
        var clientIdMissing = string.IsNullOrWhiteSpace(options.OAuthClientId);

        return (tokenMissing, clientIdMissing) switch
        {
            (tokenMissing: false, clientIdMissing: false) => ValidateOptionsResult.Success,
            (tokenMissing: true, clientIdMissing: false) => ValidateOptionsResult.Fail("GitHub:Token is required."),
            (tokenMissing: false, clientIdMissing: true) => ValidateOptionsResult.Fail("GitHub:OAuthClientId is required."),
            _ => ValidateOptionsResult.Fail("GitHub section is invalid.")
        };
    }
}