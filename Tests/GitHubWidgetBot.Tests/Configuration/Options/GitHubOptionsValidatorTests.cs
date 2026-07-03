using GitHubWidgetBot.Configuration.Options;

namespace GitHubWidgetBot.Tests.Configuration.Options;

internal sealed class GitHubOptionsValidatorTests
{
    [Test]
    public async Task Validate_MissingOAuthClientId_ReturnsSuccess()
    {
        var validator = new GitHubOptionsValidator();

        var options = new GitHubOptions { Token = "token" };
        var result = validator.Validate(null, options);

        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    public async Task Validate_MissingToken_ReturnsFailure()
    {
        var validator = new GitHubOptionsValidator();

        var options = new GitHubOptions { OAuthClientId = "client-id" };
        var result = validator.Validate(null, options);

        await Assert.That(result.Failed).IsTrue();
    }

    [Test]
    public async Task Validate_EmptyTokenAndOAuthClientId_ReturnsFailure()
    {
        var validator = new GitHubOptionsValidator();

        var options = new GitHubOptions { Token = string.Empty, OAuthClientId = string.Empty };
        var result = validator.Validate(null, options);

        await Assert.That(result.Failed).IsTrue();
    }

    [Test]
    public async Task Validate_TokenAndOAuthClientId_ReturnsSuccess()
    {
        var validator = new GitHubOptionsValidator();

        var options = new GitHubOptions { Token = "token", OAuthClientId = "client-id" };
        var result = validator.Validate(null, options);

        await Assert.That(result.Succeeded).IsTrue();
    }
}