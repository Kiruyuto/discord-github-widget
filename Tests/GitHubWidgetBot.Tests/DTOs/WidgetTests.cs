using GitHubWidgetBot.DTOs;

namespace GitHubWidgetBot.Tests.DTOs;

internal sealed class WidgetTests
{
    [Test]
    public async Task ToJson_NullProfileValuesAndLargeCounts_WritesExpectedPayload()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: "https://example.com/avatar.png",
            profileName: string.Empty,
            profileBio: string.Empty,
            contributions: 1_234_567,
            followers: 12_345,
            following: 678,
            starsTotal: 4_294_967_295,
            publicRepos: null,
            topLanguage: "C#"
        );

        var json = widget.ToJson();

        await Assert
            .That(json)
            .IsEqualTo(
                """{"username":"userUsername","data":{"dynamic":[{"type":1,"name":"user_profile_handle","value":"@userHandle"},{"type":3,"name":"user_avatar_image","value":{"url":"https://example.com/avatar.png"}},{"type":1,"name":"user_profile_name","value":""},{"type":1,"name":"user_profile_bio","value":""},{"type":1,"name":"user_contributions","value":"1 234 567"},{"type":1,"name":"user_followers","value":"12 345"},{"type":1,"name":"user_following","value":"678"},{"type":1,"name":"user_stars_total","value":"4 294 967 295"},{"type":1,"name":"user_public_repos","value":null},{"type":1,"name":"user_top_language","value":"C#"}]}}"""
            );
    }
}