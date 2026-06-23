using GitHubWidgetBot.Persistence;
using GitHubWidgetBot.Persistence.DTOs;
using GitHubWidgetBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot.Modules;

internal class ModalModule(ILogger<ModalModule> logger, GitHubService gitHubService, ApplicationDbContext dbContext) : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(ApplicationConfiguration.DiscordComponents.WidgetSetupModalId)]
    public async Task ProcessModalAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Processing setup modal interaction from @{Username} ({DiscordUserId})", Context.User.Username, userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        // TODO: If commands fails anywhere below it will throw and not return any information to the user due to deferral
        // TODO: Create custom ResultHandler

        var labelComponents = Context.Components.OfType<Label>().Select(static label => label.Component).ToArray();
        var checkbox = labelComponents.OfType<Checkbox>().FirstOrDefault();
        if (checkbox is null)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Setup modal interaction from Discord user {DiscordUserId} did not include the exclude-unknown checkbox", userId);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Invalid setup form. Please run `/setup` again."; });
            return;
        }

        var excludeUnknown = checkbox.Checked;

        var ghOauthSetupData = await dbContext.SetupSessions.FirstOrDefaultAsync(x => x.DiscordUserId == userId);
        if (ghOauthSetupData is null)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Setup modal interaction from Discord user {DiscordUserId} did not have a matching setup session", userId);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Setup session expired or could not be found. Please run `/setup` again."; });
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Found setup session for Discord user {DiscordUserId}. Exclude unknown languages: {ExcludeUnknown}; GitHub authorization expires at {Expiration}; poll interval: {PollIntervalSeconds}s",
                userId, excludeUnknown, ghOauthSetupData.GitHubExpiresAtUtc, ghOauthSetupData.GitHubPollIntervalSeconds
            );
        }

        (string Login, string AccessToken)? gitHubAuthorization = null;
        var authorizationChecks = 0;
        while (gitHubAuthorization is null && DateTimeOffset.UtcNow < ghOauthSetupData.GitHubExpiresAtUtc)
        {
            authorizationChecks++;
            gitHubAuthorization = await gitHubService.CheckDeviceAuthorizationAsync(ghOauthSetupData.GitHubDeviceCode);

            if (gitHubAuthorization is null) await Task.Delay((int)(ghOauthSetupData.GitHubPollIntervalSeconds * 1_000));
        }

        if (gitHubAuthorization is not { } authorization)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "GitHub device authorization was not completed for Discord user {DiscordUserId}. Checks: {AuthorizationChecks}; expired at {Expiration}",
                    userId, authorizationChecks, ghOauthSetupData.GitHubExpiresAtUtc
                );
            }

            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Failed to obtain GH Oauth flow token. Have you finished authorization?"; });
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "GitHub device authorization completed for Discord user {DiscordUserId} as @{GitHubUsername} after {AuthorizationChecks} checks",
                userId, authorization.Login, authorizationChecks
            );
        }

        var widget = await gitHubService.FetchUserDataAsync(authorization.Login, excludeUnknown, authorization.AccessToken);
        if (!widget.HasValue)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to build widget data for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}", userId, authorization.Login);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Failed to fetch user data from GitHub >:("; });
            return;
        }

        using var content = widget.Value.ToJsonContent();
        try
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Updating Discord widget profile for Discord user {DiscordUserId}", userId);
            await Context.Client.Rest.SendRequestAsync(
                method: HttpMethod.Patch,
                content: content,
                route: $"/applications/{Context.Client.Id}/users/{userId}/identities/0/profile"
            );
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to update Discord widget profile for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}", userId, authorization.Login);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Failed to update your Discord widget profile."; });
            return;
        }

        await Context.Interaction.ModifyResponseAsync(x =>
        {
            x.Flags = MessageFlags.Ephemeral;
            x.Content = $"Successfully validated and configured widget!\n" +
                        $"Now you should be able to add it to your Discord profile! (Restart discord => `Profile` => `Add Widget`)";
        });

        var utcNow = DateTimeOffset.UtcNow;
        dbContext.SetupSessions.Remove(ghOauthSetupData);
        var refreshTarget = await dbContext.RefreshTargets.FirstOrDefaultAsync(x => x.DiscordUserId == userId && x.GitHubUsername == authorization.Login);
        if (refreshTarget == null)
        {
            dbContext.RefreshTargets.Add(new RefreshTarget
            {
                DiscordUserId = userId,
                GitHubUsername = authorization.Login,
                LastUpdateUtc = utcNow,
                LastAttemptUtc = utcNow,
                FailureCount = 0
            });
        }
        else
        {
            refreshTarget.LastUpdateUtc = utcNow;
            refreshTarget.LastAttemptUtc = utcNow;
            refreshTarget.FailureCount = 0;
            dbContext.RefreshTargets.Update(refreshTarget);
        }

        var changed = await dbContext.SaveChangesAsync();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Completed widget setup for Discord user {DiscordUserId} as @{GitHubUsername}. Exclude unknown languages: {ExcludeUnknown}; database changes: {ChangedCount}",
                userId, authorization.Login, excludeUnknown, changed
            );
        }
    }
}