using GitHubWidgetBot.Configuration;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot.Modules;

internal sealed class ButtonModule(ILogger<ButtonModule> logger) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(ApplicationConfiguration.DiscordComponents.WidgetSetupVerifyButtonId)]
    public async Task ProcessModalAsync()
    {
        var userId = Context.User.Id;
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Processing setup verify button interaction from @{Username} ({DiscordUserId})", Context.User.Username, userId);

        var checkbox = new CheckboxProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupExcludeUnknownId) { Default = true };
        var modalProps = new ModalProperties(ApplicationConfiguration.DiscordComponents.WidgetSetupModalId, "GitHub Widget Configuration")
        {
            new LabelProperties("Exclude repositories with unknown language?", checkbox)
            {
                Description = "GitHub returns null when a repository language is not detected"
            }
        };

        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modalProps));
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Opened setup configuration modal for Discord user {DiscordUserId}", userId);
    }
}