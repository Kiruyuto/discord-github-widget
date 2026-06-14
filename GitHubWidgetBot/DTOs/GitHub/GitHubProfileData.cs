using System.Text.Json.Serialization;

namespace GitHubWidgetBot.DTOs.GitHub;

internal sealed record GitHubProfileData(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("bio")] string? Bio,
    [property: JsonPropertyName("followers")] uint Followers,
    [property: JsonPropertyName("following")] uint Following,
    [property: JsonPropertyName("public_repos")] uint PublicRepos
);