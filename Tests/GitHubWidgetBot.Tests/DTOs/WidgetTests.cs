using GitHubWidgetBot.DTOs;

namespace GitHubWidgetBot.Tests.DTOs;

internal sealed class WidgetTests
{
    [Test]
    public async Task Create_CountBoundaries_FormatsValuesWithSpaceGroups()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: "https://example.com/avatar.png",
            profileName: "User Name",
            profileBio: "Bio",
            contributions: 0,
            followers: 999,
            following: 1_000,
            starsTotal: 1_000_000,
            publicRepos: uint.MaxValue,
            topLanguage: "C#"
        );

        await Assert.That(widget.Data.Dynamic[4].Value).IsEqualTo("0");
        await Assert.That(widget.Data.Dynamic[5].Value).IsEqualTo("999");
        await Assert.That(widget.Data.Dynamic[6].Value).IsEqualTo("1 000");
        await Assert.That(widget.Data.Dynamic[7].Value).IsEqualTo("1 000 000");
        await Assert.That(widget.Data.Dynamic[8].Value).IsEqualTo("4 294 967 295");
    }

    [Test]
    public async Task Create_NullableCountsAreNull_StoresNullValues()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: "https://example.com/avatar.png",
            profileName: "User Name",
            profileBio: "Bio",
            contributions: null,
            followers: 1,
            following: 2,
            starsTotal: 3,
            publicRepos: null,
            topLanguage: "C#"
        );

        await Assert.That(widget.Data.Dynamic[4].Value).IsNull();
        await Assert.That(widget.Data.Dynamic[5].Value).IsEqualTo("1");
        await Assert.That(widget.Data.Dynamic[6].Value).IsEqualTo("2");
        await Assert.That(widget.Data.Dynamic[7].Value).IsEqualTo("3");
        await Assert.That(widget.Data.Dynamic[8].Value).IsNull();
    }

    [Test]
    public async Task Create_NullAvatar_StoresNullAvatarValue()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: null,
            profileName: "User Name",
            profileBio: "Bio",
            contributions: 1,
            followers: 2,
            following: 3,
            starsTotal: 4,
            publicRepos: 5,
            topLanguage: "C#"
        );

        var avatarEntry = widget.Data.Dynamic[1];
        await Assert.That(avatarEntry.Type).IsEqualTo(3u);
        await Assert.That(avatarEntry.Name).IsEqualTo("user_avatar_image");
        await Assert.That(avatarEntry.Value).IsNull();
    }

    [Test]
    public async Task Create_ProfilePayload_KeepsDynamicEntriesInExpectedOrder()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: "https://example.com/avatar.png",
            profileName: "User Name",
            profileBio: "Bio",
            contributions: 1,
            followers: 2,
            following: 3,
            starsTotal: 4,
            publicRepos: 5,
            topLanguage: "C#"
        );

        await Assert.That(widget.Data.Dynamic.Length).IsEqualTo(10);
        await Assert.That(widget.Data.Dynamic[0].Name).IsEqualTo("user_profile_handle");
        await Assert.That(widget.Data.Dynamic[1].Name).IsEqualTo("user_avatar_image");
        await Assert.That(widget.Data.Dynamic[2].Name).IsEqualTo("user_profile_name");
        await Assert.That(widget.Data.Dynamic[3].Name).IsEqualTo("user_profile_bio");
        await Assert.That(widget.Data.Dynamic[4].Name).IsEqualTo("user_contributions");
        await Assert.That(widget.Data.Dynamic[5].Name).IsEqualTo("user_followers");
        await Assert.That(widget.Data.Dynamic[6].Name).IsEqualTo("user_following");
        await Assert.That(widget.Data.Dynamic[7].Name).IsEqualTo("user_stars_total");
        await Assert.That(widget.Data.Dynamic[8].Name).IsEqualTo("user_public_repos");
        await Assert.That(widget.Data.Dynamic[9].Name).IsEqualTo("user_top_language");
    }

    [Test]
    public async Task ToJsonContent_WidgetPayload_ReturnsApplicationJsonContentType()
    {
        var widget = Widget.Create(
            username: "userUsername",
            profileHandle: "userHandle",
            avatarImage: "https://example.com/avatar.png",
            profileName: "User Name",
            profileBio: "Bio",
            contributions: 1,
            followers: 2,
            following: 3,
            starsTotal: 4,
            publicRepos: 5,
            topLanguage: "C#"
        );

        using var content = widget.ToJsonContent();

        await Assert.That(content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
    }

    [Test]
    public async Task ToJsonContent_NullProfileValuesAndLargeCounts_WritesExpectedPayload()
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

        var json = await ReadJsonAsync(widget);
        const string Expected = """
                                {"username":"userUsername","data":{"dynamic":[{"type":1,"name":"user_profile_handle","value":"@userHandle"},{"type":3,"name":"user_avatar_image","value":{"url":"https://example.com/avatar.png"}},{"type":1,"name":"user_profile_name","value":""},{"type":1,"name":"user_profile_bio","value":""},{"type":1,"name":"user_contributions","value":"1 234 567"},{"type":1,"name":"user_followers","value":"12 345"},{"type":1,"name":"user_following","value":"678"},{"type":1,"name":"user_stars_total","value":"4 294 967 295"},{"type":1,"name":"user_public_repos","value":null},{"type":1,"name":"user_top_language","value":"C#"}]}}
                                """;

        await Assert.That(json).IsEqualTo(Expected);
    }

    private static async Task<string> ReadJsonAsync(Widget widget)
    {
        using var content = widget.ToJsonContent();
        return await content.ReadAsStringAsync();
    }
}