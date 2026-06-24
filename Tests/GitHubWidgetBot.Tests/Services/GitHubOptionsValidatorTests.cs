using GitHubWidgetBot.Services;

namespace GitHubWidgetBot.Tests.Services;

internal sealed class GitHubOptionsValidatorTests
{
    [Test]
    public async Task Validate_MissingOAuthClientId_ReturnsSuccess()
    {
        var validator = new GitHubOptionsValidator();

        var result = validator.Validate(null, new GitHubOptions { Token = "token" });

        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    public async Task Validate_MissingToken_ReturnsFailure()
    {
        var validator = new GitHubOptionsValidator();

        var result = validator.Validate(null, new GitHubOptions { OAuthClientId = "client-id" });

        await Assert.That(result.Failed).IsTrue();
    }
}