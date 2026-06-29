using GitHubWidgetBot.Persistence;
using GitHubWidgetBot.Persistence.DTOs;
using GitHubWidgetBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot.Modules;

internal sealed class ModalsModule(ILogger<ModalsModule> logger, GitHubService gitHubService, ApplicationDbContext dbContext) : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(ApplicationConfiguration.DiscordComponents.WidgetSetupModalId)]
    public async Task ProcessModalAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Processing setup modal interaction from @{Username} ({DiscordUserId})", Context.User.Username, userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (!TryGetExcludeUnknown(Context.Components, out var excludeUnknown))
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Setup modal interaction from Discord user {DiscordUserId} did not include the exclude-unknown checkbox", userId);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Invalid setup form",
                body: "Please run `/setup` again.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        var ghOauthSetupData = await dbContext.SetupSessions.FirstOrDefaultAsync(x => x.DiscordUserId == userId);
        if (ghOauthSetupData is null)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Setup modal interaction from Discord user {DiscordUserId} did not have a matching setup session", userId);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Setup session expired",
                body: "The setup session expired or could not be found. Please run `/setup` again.",
                flags: MessageFlags.Ephemeral
            ));
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

            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# GitHub authorization incomplete",
                body: "Failed to obtain a GitHub OAuth flow token. Confirm authorization on GitHub, then try setup again.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "GitHub device authorization completed for Discord user {DiscordUserId} as @{GitHubUsername} after {AuthorizationChecks} checks",
                userId, authorization.Login, authorizationChecks
            );
        }

        if (await ConfigureWidgetAsync(userId: userId, gitHubUsername: authorization.Login, excludeUnknown: excludeUnknown, token: authorization.AccessToken))
        {
            dbContext.SetupSessions.Remove(ghOauthSetupData);
            var changed = await dbContext.SaveChangesAsync();
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Removed setup session for Discord user {DiscordUserId}; database changes: {ChangedCount}", userId, changed);
        }
    }

    [ComponentInteraction(ApplicationConfiguration.DiscordComponents.WidgetSetupManualModalId)]
    public async Task ProcessManualModalAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Processing manual setup modal interaction from @{Username} ({DiscordUserId})", Context.User.Username, userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (!TryGetManualSetupValues(Context.Components, out var excludeUnknown, out var username))
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Manual setup modal interaction from Discord user {DiscordUserId} did not include the exclude-unknown checkbox", userId);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Invalid setup form",
                body: "Please run `/setup-manual` again.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Manual setup modal interaction from Discord user {DiscordUserId} did not include a GitHub username", userId);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Invalid setup form",
                body: "Please enter a GitHub username.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        await ConfigureWidgetAsync(userId: userId, gitHubUsername: username, excludeUnknown: excludeUnknown, token: null);
    }

    private async Task<bool> ConfigureWidgetAsync(ulong userId, string gitHubUsername, bool excludeUnknown, string? token)
    {
        var widget = await gitHubService.FetchUserDataAsync(gitHubUsername, excludeUnknown, token);
        if (!widget.HasValue)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to build widget data for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}", userId, gitHubUsername);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Could not fetch GitHub data",
                body: "Failed to fetch user data from GitHub.",
                flags: MessageFlags.Ephemeral
            ));
            return false;
        }

        // GitHub username lookup is case-insensitive, but the API returns the canonical login casing.
        // Normalize here instead of adding weird CKs, changing type to citext, or another workaround.
        var normalizedGitHubUsername = widget.Value.Data.Username;
        using var content = widget.Value.ToJsonContent();
        try
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Updating Discord widget profile for Discord user {DiscordUserId}", userId);
            await Context.Client.Rest.SendRequestAsync(
                method: HttpMethod.Patch,
                content: content,
                route: $"/applications/{Context.Client.Id}/users/{userId}/identities/{userId}/profile"
            );
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to update Discord widget profile for Discord user {DiscordUserId} and GitHub user @{GitHubUsername}", userId, normalizedGitHubUsername);
            await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyErrorCard(
                x,
                heading: "# Could not update Discord widget",
                body: "Failed to update your Discord widget profile.",
                flags: MessageFlags.Ephemeral
            ));
            return false;
        }

        await Context.Interaction.ModifyResponseAsync(static x => InteractionResponseBuilder.ApplyCard(
            x,
            heading: "# Widget configured",
            body: "Successfully validated and configured your widget.\nRestart Discord, open `Profile`, then choose `Add Widget`.",
            flags: MessageFlags.Ephemeral
        ));

        var utcNow = DateTimeOffset.UtcNow;
        var refreshTarget = await dbContext.RefreshTargets.FirstOrDefaultAsync(target => target.DiscordUserId == userId);
        if (refreshTarget == null)
        {
            dbContext.RefreshTargets.Add(new RefreshTarget
            {
                DiscordUserId = userId,
                GitHubUsername = normalizedGitHubUsername,
                ExcludeUnknown = excludeUnknown,
                LastUpdateUtc = utcNow,
                LastAttemptUtc = utcNow,
                FailureCount = 0
            });
        }
        else
        {
            refreshTarget.GitHubUsername = normalizedGitHubUsername;
            refreshTarget.ExcludeUnknown = excludeUnknown;
            refreshTarget.RecordSuccessfulAttempt(utcNow);
            dbContext.RefreshTargets.Update(refreshTarget);
        }

        var changed = await dbContext.SaveChangesAsync();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Completed widget setup for Discord user {DiscordUserId} as @{GitHubUsername}. Exclude unknown languages: {ExcludeUnknown}; database changes: {ChangedCount}",
                userId, normalizedGitHubUsername, excludeUnknown, changed
            );
        }

        return true;
    }

    private static bool TryGetExcludeUnknown(IReadOnlyList<IModalComponent> components, out bool excludeUnknown)
    {
        for (var i = 0; i < components.Count; i++)
        {
            if (components[i] is Label { Component: Checkbox { CustomId: ApplicationConfiguration.DiscordComponents.WidgetSetupExcludeUnknownId } checkbox })
            {
                excludeUnknown = checkbox.Checked;
                return true;
            }
        }

        excludeUnknown = false;
        return false;
    }

    private static bool TryGetManualSetupValues(IReadOnlyList<IModalComponent> components, out bool excludeUnknown, out string? username)
    {
        Checkbox? excludeUnknownCheckbox = null;
        TextInput? usernameInput = null;

        for (var i = 0; i < components.Count; i++)
        {
            if (components[i] is not Label label) continue;

            switch (label.Component)
            {
                case Checkbox checkbox when excludeUnknownCheckbox is null && checkbox.CustomId == ApplicationConfiguration.DiscordComponents.WidgetSetupExcludeUnknownId:
                    excludeUnknownCheckbox = checkbox;
                    break;

                case TextInput textInput when usernameInput is null && textInput.CustomId == ApplicationConfiguration.DiscordComponents.WidgetSetupManualGitHubUsernameId:
                    usernameInput = textInput;
                    break;
            }

            if (excludeUnknownCheckbox is not null && usernameInput is not null) break;
        }

        if (excludeUnknownCheckbox is null)
        {
            excludeUnknown = false;
            username = null;
            return false;
        }

        excludeUnknown = excludeUnknownCheckbox.Checked;
        username = usernameInput?.Value.Trim();
        return true;
    }
}