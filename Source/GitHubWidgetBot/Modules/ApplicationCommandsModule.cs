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

internal class ApplicationCommandsModule(
    ILogger<ApplicationCommandsModule> logger,
    GitHubService gitHubService,
    ApplicationDbContext dbContext,
    IOptions<DiscordOptions> discordOptions
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(name: "setup", description: "Setup or refresh your widget using GitHub OAuth2 flow")]
    public async Task SetupAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Starting widget setup for @{Username} ({DiscordUserId})", Context.User.Username, userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Deferred setup response for Discord user {DiscordUserId}", userId);

        var permissionResult = await GetConfigurationPermissionAsync(userId);
        if (!permissionResult)
        {
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Only the Discord application owner or team members can configure this widget."; });
            return;
        }

        if (!gitHubService.IsOAuthDeviceFlowConfigured)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("GitHub OAuth Device Flow is not configured. Cannot proceed with setup for Discord user {DiscordUserId}", userId);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "GitHub OAuth Device Flow is not configured. Set `GitHub__OAuthClientId` and restart the bot to use `/setup`."; });
            return;
        }

        var gitHubDeviceFlow = await gitHubService.StartDeviceAuthorizationAsync();
        if (gitHubDeviceFlow is null)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to start GitHub device authorization for Discord user {DiscordUserId}", userId);
            await Context.Interaction.ModifyResponseAsync(x => { x.Content = "Failed to initiate OAuth2 Device Flow."; });
            return;
        }

        const string DiscordSetupInstr =
            "## Discord (Optional if you have already done it)\n" +
            "First, authorize this Discord application with the `[Authorize Discord]` button below.\n" +
            "Discord may show a broad permission list even though this app only uses the `sdk.social_layer` scope. " +
            "That is how Discord profile widgets are authorized.\n" +
            "**This app does not store your Discord token or use those permissions for anything beyond widget setup.**";

        const string GithubSetupInstr =
            "## GitHub\n" +
            "Next, confirm ownership of your GitHub account with the `[Authorize GitHub]` button below.\n" +
            "GitHub will ask you to enter the verification code shown here because this setup uses OAuth2 Device Flow.\n" +
            "This app only requests public GitHub data so it can confirm your identity.\n" +
            "**This app does not store your GitHub token. It is only used during setup, and the token expires after 15 minutes.**\n" +
            "After setup, you can remove this app from your GitHub account under [Authorized OAuth Apps](https://github.com/settings/applications).";

        const string SummaryInstr =
            "## Finish setup\n" +
            "When both authorizations are complete, press `[Verify]`.\n" +
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

        await Context.Interaction.ModifyResponseAsync(options =>
        {
            options.Flags = MessageFlags.IsComponentsV2 | MessageFlags.Ephemeral;
            options.Components = new[]
            {
                new ComponentContainerProperties
                {
                    AccentColor = new Color(red: 88, green: 101, blue: 242),
                    Components = new IComponentContainerComponentProperties[]
                    {
                        new TextDisplayProperties(content: "# GitHub Widget Setup instructions"),
                        new TextDisplayProperties(DiscordSetupInstr),
                        new TextDisplayProperties(GithubSetupInstr),
                        new TextDisplayProperties(content: $"**This setup session expires <t:{gitHubDeviceFlow.ExpiresAt.ToUnixTimeSeconds()}:R>.**"),
                        new TextDisplayProperties(content: $"GitHub verification code:\n```{gitHubDeviceFlow.UserCode}```"),
                        new TextDisplayProperties(SummaryInstr),
                        new ComponentSeparatorProperties { Divider = true, Spacing = ComponentSeparatorSpacingSize.Small },
                        new TextDisplayProperties(content: "-# Source code for the app is [available on GitHub](https://github.com/Kiruyuto/discord-github-widget)"),
                        new ActionRowProperties
                        {
                            new LinkButtonProperties(discordOptions.Value.AuthorizeUrl, "[Authorize Discord]"),
                            new LinkButtonProperties(gitHubDeviceFlow.VerificationUri, "[Authorize GitHub]"),
                            new ButtonProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupVerifyButtonId, "[Verify]", ButtonStyle.Primary)
                        }
                    }
                }
            };
        });

        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Sent setup instructions to Discord user {DiscordUserId}", userId);
    }

    [SlashCommand(name: "setup-manual", description: "Setup or refresh your widget by entering a GitHub username")]
    public async Task SetupManualAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Starting manual widget setup for @{Username} ({DiscordUserId})", Context.User.Username, userId);

        var permissionResult = await GetConfigurationPermissionAsync(userId);
        if (!permissionResult)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Content = "Only the Discord application owner or team members can configure this widget."
            }));
            return;
        }

        var usernameInput = new TextInputProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupManualGitHubUsernameId, TextInputStyle.Short)
        {
            Placeholder = "Kiruyuto",
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

    [SlashCommand(name: "invite", description: "Share the Discord authorization URL for this widget")]
    public Task InviteAsync(User user)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Sending public invite URL to @{Username} ({DiscordUserId})", Context.User.Username, Context.User.Id);

        return Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Flags = MessageFlags.SuppressEmbeds,
            AllowedMentions = AllowedMentionsProperties.All,
            Content = $"{user}\n\n" +
                      "To use this widget:\n" +
                      $"1. Authorize the Discord app with [this link]({discordOptions.Value.AuthorizeUrl}).\n" +
                      "2. Ask the app owner to add you to the Discord Developer Team that owns this app. " +
                      "They can do that in the [Developer Portal](https://discord.com/developers/teams)."
        }));
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