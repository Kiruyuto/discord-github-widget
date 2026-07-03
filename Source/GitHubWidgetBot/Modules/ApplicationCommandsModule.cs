using GitHubWidgetBot.Configuration;
using GitHubWidgetBot.Configuration.Options;
using GitHubWidgetBot.Persistence;
using GitHubWidgetBot.Persistence.DTOs;
using GitHubWidgetBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GitHubWidgetBot.Modules;

internal sealed class ApplicationCommandsModule(
    ILogger<ApplicationCommandsModule> logger,
    GitHubService gitHubService,
    ApplicationDbContext dbContext,
    IOptions<DiscordOptions> discordOptions
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(name: ApplicationConfiguration.DiscordCommands.SetupName, description: "Setup or refresh your widget using GitHub OAuth2 flow")]
    public async Task SetupAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Starting widget setup for @{Username} ({DiscordUserId})", Context.User.Username, userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Deferred setup response for Discord user {DiscordUserId}", userId);

        var allowedToConfigure = await GetConfigurationPermissionAsync(userId);
        if (!allowedToConfigure)
        {
            await Context.Interaction.ModifyResponseAsync(static options => InteractionResponseBuilder.ApplyErrorCard(
                options: options,
                heading: "# Configuration access denied",
                body: "Only the Discord application owner or team members can configure this widget.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        if (!gitHubService.IsOAuthDeviceFlowConfigured)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("GitHub OAuth Device Flow is not configured. Cannot proceed with setup for Discord user {DiscordUserId}", userId);
            await Context.Interaction.ModifyResponseAsync(static options => InteractionResponseBuilder.ApplyErrorCard(
                options: options,
                heading: "# GitHub OAuth is not configured",
                body: "Set `GitHub__OAuthClientId` and restart the bot to use `/setup`.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        var gitHubDeviceFlow = await gitHubService.StartDeviceAuthorizationAsync();
        if (gitHubDeviceFlow is null)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to start GitHub device authorization for Discord user {DiscordUserId}", userId);
            await Context.Interaction.ModifyResponseAsync(static options => InteractionResponseBuilder.ApplyErrorCard(
                options: options,
                heading: "# Could not start GitHub authorization",
                body: "Failed to initiate OAuth2 Device Flow.",
                flags: MessageFlags.Ephemeral
            ));
            return;
        }

        const string AuthDiscordBtnLabel = "Authorize Discord";
        const string DiscordSetupInstr =
            "## Discord\n" +
            $"First, authorize this Discord application with the `{AuthDiscordBtnLabel}` button below.\n" +
            "Discord may show a broad permission list even though this app only uses the `sdk.social_layer` scope. " +
            "That is how Discord profile widgets are authorized.\n" +
            "**This app does not store your Discord token or use those permissions for anything beyond widget setup.**";

        const string AuthGitHubBtnLabel = "Authorize GitHub";
        const string GitHubSetupInstr =
            "## GitHub\n" +
            $"Next, confirm ownership of your GitHub account with the `{AuthGitHubBtnLabel}` button below.\n" +
            "GitHub will ask you to enter the verification code shown here because this setup uses OAuth2 Device Flow.\n" +
            "This app only requests public GitHub data so it can confirm your identity.\n" +
            "**This app does not store your GitHub token. It is only used during setup, and the token expires after 15 minutes.**\n" +
            "After setup, you can remove this app from your GitHub account under [Authorized OAuth Apps](https://github.com/settings/applications).";

        const string VerifyBtnLabel = "Verify";
        const string SummaryInstr =
            "## Finish setup\n" +
            $"When both authorizations are complete, press `{VerifyBtnLabel}`.\n" +
            "If verification succeeds, a short configuration form will open so you can fine-tune the widget.\n" +
            "If verification fails, this message will update with the reason.";

        var replacedSessions = await dbContext.SetupSessions.Where(x => x.DiscordUserId == userId).ExecuteDeleteAsync();
        if (replacedSessions != 0 && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Replaced {ReplacedSessionCount} stale setup session(s) for Discord user {DiscordUserId}", replacedSessions, userId);

        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Adding setup session for Discord user {DiscordUserId}. GitHub device flow expires at {Expiration}", userId, gitHubDeviceFlow.ExpiresAt);

        dbContext.SetupSessions.Add(new SetupSession
        {
            DiscordUserId = userId,
            GitHubDeviceCode = gitHubDeviceFlow.DeviceCode,
            GitHubPollIntervalSeconds = gitHubDeviceFlow.PollIntervalSeconds,
            GitHubExpiresAtUtc = gitHubDeviceFlow.ExpiresAt
        });

        var changed = await dbContext.SaveChangesAsync();
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Created setup session for Discord user {DiscordUserId}; expires at {Expiration}; database changes: {ChangedCount}", userId, gitHubDeviceFlow.ExpiresAt, changed);

        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Sending setup instructions to Discord user {DiscordUserId}", userId);

        var expiresAt = gitHubDeviceFlow.ExpiresAt.ToUnixTimeSeconds();
        var setupInstructions = $"""
                                 {DiscordSetupInstr}

                                 {GitHubSetupInstr}

                                 **This setup session expires <t:{expiresAt}:R>.**

                                 GitHub verification code:
                                 ```{gitHubDeviceFlow.UserCode}```

                                 {SummaryInstr}
                                 """;

        await Context.Interaction.ModifyResponseAsync(options => InteractionResponseBuilder.ApplyCard(
            options,
            heading: "# GitHub Widget Setup",
            body: setupInstructions,
            flags: MessageFlags.Ephemeral,
            actions:
            [
                new LinkButtonProperties(discordOptions.Value.AuthorizeUrl, AuthDiscordBtnLabel),
                new LinkButtonProperties(gitHubDeviceFlow.VerificationUrl, AuthGitHubBtnLabel),
                new ButtonProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupVerifyButtonId, VerifyBtnLabel, ButtonStyle.Primary)
            ]
        ));

        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Sent setup instructions to Discord user {DiscordUserId}", userId);
    }

    [SlashCommand(name: ApplicationConfiguration.DiscordCommands.SetupManualName, description: "Setup or refresh your widget by entering a GitHub username")]
    public async Task SetupManualAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Starting manual widget setup for @{Username} ({DiscordUserId})", Context.User.Username, userId);

        var allowedToConfigure = await GetConfigurationPermissionAsync(userId);
        if (!allowedToConfigure)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionResponseBuilder.CreateErrorCard(
                heading: "# Configuration access denied",
                body: "Only the Discord application owner or team members can configure this widget.",
                flags: MessageFlags.Ephemeral
            )));
            return;
        }

        var usernameInput = new TextInputProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupManualGitHubUsernameId, TextInputStyle.Short)
        {
            Placeholder = "Octocat",
            MinLength = 1,
            MaxLength = 39,
            Required = true
        };
        var checkbox = new CheckboxProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupExcludeUnknownId)
        {
            Default = true
        };

        var modalProps = new ModalProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupManualModalId, "GitHub Widget Manual Setup")
        {
            new LabelProperties("GitHub username", usernameInput) { Description = "Use the GitHub Account handle, not the profile display name" },
            new LabelProperties("Exclude repositories with unknown language?", checkbox) { Description = "GitHub returns null when a repository language is not detected" }
        };

        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modalProps));
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Opened manual setup modal for Discord user {DiscordUserId}", userId);
    }

    [SlashCommand(name: ApplicationConfiguration.DiscordCommands.InviteName, description: "Share the Discord authorization URL for this widget")]
    public async Task InviteAsync(User user)
    {
        if (user.Id == Context.User.Id)
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Discord user {DiscordUserId} tried to invite themselves", Context.User.Id);
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionResponseBuilder.CreateErrorCard(
                heading: "# Invite someone else",
                body: "You cannot invite yourself. Choose a different Discord user to invite.",
                flags: MessageFlags.Ephemeral
            )));
            return;
        }

        if (user.IsBot || user.Id == Context.Client.Id)
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Discord user {DiscordUserId} tried to invite bot", Context.User.Id);
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionResponseBuilder.CreateErrorCard(
                heading: "# Invite someone else",
                body: $"You cannot invite bots & applications. Choose a {Format.Italic("living")} user to invite.",
                flags: MessageFlags.Ephemeral
            )));
            return;
        }

        var allowedToConfigure = await GetConfigurationPermissionAsync(Context.User.Id);
        if (!allowedToConfigure)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionResponseBuilder.CreateErrorCard(
                heading: "# Configuration access denied",
                body: "Only the Discord application owner, team owner, or team admins/developers can invite users to configure this widget. " +
                      "If you should have access, ask the app owner or a team admin to add you in the [Developer Portal](https://discord.com/developers/teams).",
                flags: MessageFlags.Ephemeral
            )));
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Sending public invite URL to @{Username} ({DiscordUserId})", Context.User.Username, Context.User.Id);

        const string AuthDiscordBtnLabel = "Authorize Discord";
        var inviteInstructions = $"""
                                  Hi {user},
                                  {Context.User} invited you to set up this widget.
                                  1. Make sure you own the Discord application for this widget or have accepted a team invite for it.
                                  2. Select the `{AuthDiscordBtnLabel}` button to authorize the Discord app.
                                  3. Run `/{ApplicationConfiguration.DiscordCommands.SetupName}` or `/{ApplicationConfiguration.DiscordCommands.SetupManualName}` and follow instructions on screen
                                  """;

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionResponseBuilder.CreateCard(
            heading: "# Discord authorization",
            body: inviteInstructions,
            actions: [new LinkButtonProperties(discordOptions.Value.AuthorizeUrl, AuthDiscordBtnLabel)]
        )));
    }

    private async Task<bool> GetConfigurationPermissionAsync(ulong userId)
    {
        var app = await Context.Client.Rest.GetCurrentApplicationAsync();

        if (app.Owner?.Id == userId) return true;
        if (app.Team is null) return false;
        if (app.Team.OwnerId == userId) return true;

        foreach (var user in app.Team.Users)
        {
            if (user.Id == userId && user.Role is TeamRole.Developer or TeamRole.Admin)
                return true;
        }

        if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Discord user {DiscordUserId} is not allowed to configure this widget", userId);
        return false;
    }
}