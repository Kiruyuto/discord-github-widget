using NetCord;
using NetCord.Rest;

namespace GitHubWidgetBot;

internal static class InteractionResponseBuilder
{
    private static readonly Color DefaultAccentColor = new(red: 35, green: 135, blue: 55);
    private static readonly Color ErrorAccentColor = new(red: 250, green: 80, blue: 75);

    #region Create

    public static InteractionMessageProperties CreateCard(string heading, string body, MessageFlags flags = 0, IActionRowComponentProperties[]? actions = null)
        => CreateCard(accentColor: DefaultAccentColor, heading: heading, body: body, flags: flags, actions: actions);

    public static InteractionMessageProperties CreateErrorCard(string heading, string body, MessageFlags flags = 0)
        => CreateCard(accentColor: ErrorAccentColor, heading: heading, body: body, flags: flags, actions: null);

    #endregion

    #region Apply

    public static void ApplyCard(MessageOptions options, string heading, string body, MessageFlags flags = 0, IActionRowComponentProperties[]? actions = null)
        => ApplyCard(options: options, accentColor: DefaultAccentColor, heading: heading, body: body, flags: flags, actions: actions);

    public static void ApplyErrorCard(MessageOptions options, string heading, string body, MessageFlags flags = 0)
        => ApplyCard(options: options, accentColor: ErrorAccentColor, heading: heading, body: body, flags: flags, actions: null);

    #endregion

    #region Helpers

    private static InteractionMessageProperties CreateCard(Color accentColor, string heading, string body, MessageFlags flags, IActionRowComponentProperties[]? actions) => new()
    {
        Flags = MessageFlags.IsComponentsV2 | flags,
        AllowedMentions = AllowedMentionsProperties.All,
        Components = CreateComponents(accentColor, heading, body, actions)
    };

    private static void ApplyCard(MessageOptions options, Color accentColor, string heading, string body, MessageFlags flags, IActionRowComponentProperties[]? actions)
    {
        options.Content = null;
        options.Flags = MessageFlags.IsComponentsV2 | flags;
        options.AllowedMentions = AllowedMentionsProperties.All;
        options.Components = CreateComponents(accentColor, heading, body, actions);
    }

    [SuppressMessage("Style", "IDE0045:Convert to conditional expression")]
    private static IMessageComponentProperties[] CreateComponents(Color accentColor, string heading, string body, IActionRowComponentProperties[]? actions)
    {
        IComponentContainerComponentProperties[] components;
        if (actions is { Length: > 0 })
        {
            components =
            [
                new TextDisplayProperties(content: heading),
                new TextDisplayProperties(content: body),
                new ActionRowProperties(actions),
                new ComponentSeparatorProperties { Divider = true, Spacing = ComponentSeparatorSpacingSize.Small },
                new TextDisplayProperties(content: ApplicationConfiguration.SourceCodeFooter)
            ];
        }
        else
        {
            components =
            [
                new TextDisplayProperties(content: heading),
                new TextDisplayProperties(content: body),
                new ComponentSeparatorProperties { Divider = true, Spacing = ComponentSeparatorSpacingSize.Small },
                new TextDisplayProperties(content: ApplicationConfiguration.SourceCodeFooter)
            ];
        }

        return [new ComponentContainerProperties { AccentColor = accentColor, Components = components }];
    }

    #endregion
}