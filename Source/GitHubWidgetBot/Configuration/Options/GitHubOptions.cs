using Microsoft.Extensions.Options;

namespace GitHubWidgetBot.Configuration.Options;

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
        return string.IsNullOrWhiteSpace(options.Token)
            ? ValidateOptionsResult.Fail("GitHub:Token is required.")
            : ValidateOptionsResult.Success;
    }
}