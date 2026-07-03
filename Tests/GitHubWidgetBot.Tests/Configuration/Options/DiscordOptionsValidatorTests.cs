using GitHubWidgetBot.Configuration.Options;

namespace GitHubWidgetBot.Tests.Configuration.Options;

internal sealed class DiscordOptionsValidatorTests
{
    [Test]
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded")]
    public async Task Validate_AuthorizeUrl_ReturnsSuccess()
    {
        var validator = new DiscordOptionsValidator();

        var options = new DiscordOptions { AuthorizeUrl = "https://discord.com/oauth2/authorize" };
        var result = validator.Validate(null, options);

        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    public async Task Validate_MissingAuthorizeUrl_ReturnsFailure()
    {
        var validator = new DiscordOptionsValidator();

        var options = new DiscordOptions();
        var result = validator.Validate(null, options);

        await Assert.That(result.Failed).IsTrue();
    }

    [Test]
    public async Task Validate_EmptyAuthorizeUrl_ReturnsFailure()
    {
        var validator = new DiscordOptionsValidator();

        var options = new DiscordOptions { AuthorizeUrl = string.Empty };
        var result = validator.Validate(null, options);

        await Assert.That(result.Failed).IsTrue();
    }
}