using Microsoft.Extensions.Options;

namespace GitHubWidgetBot.Configuration.Options;

internal sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string AuthorizeUrl { get; init; } = string.Empty;
}

internal sealed class DiscordOptionsValidator : IValidateOptions<DiscordOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscordOptions options)
    {
        return string.IsNullOrWhiteSpace(options.AuthorizeUrl)
            ? ValidateOptionsResult.Fail("Discord:AuthorizeUrl is required.")
            : ValidateOptionsResult.Success;
    }
}