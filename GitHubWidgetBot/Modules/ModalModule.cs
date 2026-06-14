using GitHubWidgetBot.DTOs;
using GitHubWidgetBot.Services;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot.Modules;

internal class ModalModule(ILogger<ModalModule> logger, GitHubService gitHubService) : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(GlobalConstants.WidgetSetupModalId)]
    public async Task ProcessModalAsync()
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Processing modal interaction from @{Username}", Context.User.Username);
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        // TODO: If commands fails anywhere below it will throw and not return any information to the user due to deferral
        // TODO: Create custom ResultHandler

        // Name should meet min/max length standards set in model configuration - No need to check for that
        var labelComponents = Context.Components.OfType<Label>().Select(static label => label.Component).ToArray();
        var githubUsername = labelComponents.OfType<TextInput>().First().Value.Trim();
        var excludeUnknown = labelComponents.OfType<Checkbox>().First().Checked;

        var widgetManual = Widget.Create(
            username: Context.User.Username,
            profileHandle: githubUsername,
            avatarImage: string.Empty,
            profileName: githubUsername,
            profileBio: string.Empty,
            contributions: 0,
            followers: 0,
            following: 0,
            starsTotal: 0,
            publicRepos: 0,
            topLanguage: string.Empty
        );

        var widget = await gitHubService.FetchUserDataAsync(githubUsername, excludeUnknown);
        if (!widget.HasValue)
        {
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Failed to fetch user data from GitHub >:("; });
            return;
        }

        using var content = widget.Value.ToJsonContent();
        await Context.Client.Rest.SendRequestAsync(
            method: HttpMethod.Patch,
            content: content,
            route: $"/applications/{Context.Client.Id}/users/{Context.User.Id}/identities/0/profile"
        );

        var widgetJson = widget.Value.ToJson();

        await Context.Interaction.ModifyResponseAsync(x =>
        {
            x.Flags = MessageFlags.Ephemeral;
            x.Content = $"```json\n{widgetManual.ToJson()}\n```\n```json\n{widgetJson}```";
        });
    }
}