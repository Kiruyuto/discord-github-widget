using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GitHubWidgetBot.Modules;

internal class SetupModule(ILogger<SetupModule> logger) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(name: "setup", description: "Setup or refresh your widget. Run `/faq` to learn more.")]
    public Task SetupAsync()
    {
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Creating setup form for user @{User} (ID: {UserId})", Context.User.Username, Context.User.Id);

        var textInput = new TextInputProperties(GlobalConstants.WidgetSetupAccountNameId, TextInputStyle.Short)
        {
            Placeholder = "Example: 'octocat', 'Kiruyuto'",
            Required = true,
            MinLength = 1,
            MaxLength = 50
        };

        var checkbox = new CheckboxProperties(GlobalConstants.WidgetSetupExcludeUnknownId)
        {
            Default = true
        };

        var modalProps = new ModalProperties(GlobalConstants.WidgetSetupModalId, "GitHub Widget Setup")
        {
            new LabelProperties("Your GitHub account name?", textInput)
            {
                Description = "Provide your GitHub account handle without the '@'"
            },
            new LabelProperties("Exclude repositories with unknown language?", checkbox)
            {
                Description = "GitHub returns null when a repository language is not detected"
            }
        };

        return Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modalProps));
    }
}