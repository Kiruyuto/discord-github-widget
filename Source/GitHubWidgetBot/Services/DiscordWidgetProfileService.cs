using GitHubWidgetBot.DTOs;
using NetCord.Rest;

namespace GitHubWidgetBot.Services;

internal sealed class DiscordWidgetProfileService(RestClient restClient)
{
    public async Task UpdateProfileAsync(ulong applicationId, ulong discordUserId, Widget widget, CancellationToken cancellationToken = default)
    {
        using var content = widget.ToJsonContent();
        await restClient.SendRequestAsync(
            method: HttpMethod.Patch,
            content: content,
            route: $"/applications/{applicationId}/users/{discordUserId}/identities/{discordUserId}/profile",
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}