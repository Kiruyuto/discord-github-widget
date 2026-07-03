using NetCord;
using NetCord.Rest;

namespace GitHubWidgetBot.Tests;

internal sealed class InteractionResponseBuilderTests
{
    private const string ExpectedFooter = "-# Source code for the app is [available on GitHub](https://github.com/Kiruyuto/discord-github-widget)";

    [Test]
    public async Task CreateCard_InputFlags_IncludesComponentsV2AndPreservesInputFlags()
    {
        var message = InteractionResponseBuilder.CreateCard(
            heading: "# Heading",
            body: "Body",
            flags: MessageFlags.Ephemeral
        );

        await Assert.That(message.Flags).IsEqualTo(MessageFlags.IsComponentsV2 | MessageFlags.Ephemeral);
    }

    [Test]
    public async Task CreateCard_NoActions_BuildsContainerWithHeadingBodyFooterAndNoActionRow()
    {
        var message = InteractionResponseBuilder.CreateCard(
            heading: "# Heading",
            body: "Body"
        );

        await Assert.That(message.Components).IsNotNull();
        IMessageComponentProperties[] topLevelComponents = [.. message.Components!];
        await Assert.That(topLevelComponents.Length).IsEqualTo(1);

        var container = (ComponentContainerProperties)topLevelComponents[0];
        IComponentContainerComponentProperties[] components = [.. container.Components!];
        await Assert.That(components.Length).IsEqualTo(4);

        var heading = (TextDisplayProperties)components[0];
        await Assert.That(heading.Content).IsEqualTo("# Heading");

        var body = (TextDisplayProperties)components[1];
        await Assert.That(body.Content).IsEqualTo("Body");

        var separator = (ComponentSeparatorProperties)components[2];
        await Assert.That(separator.Divider).IsTrue();
        await Assert.That(separator.Spacing).IsEqualTo(ComponentSeparatorSpacingSize.Small);

        var footer = (TextDisplayProperties)components[3];
        await Assert.That(footer.Content).IsEqualTo(ExpectedFooter);
    }

    [Test]
    public async Task CreateCard_WithActions_IncludesActionRowBeforeFooter()
    {
        IActionRowComponentProperties[] actions = [new ButtonProperties("verify-button", "Verify", ButtonStyle.Primary)];

        var message = InteractionResponseBuilder.CreateCard(
            heading: "# Heading",
            body: "Body",
            actions: actions
        );

        await Assert.That(message.Components).IsNotNull();
        IMessageComponentProperties[] topLevelComponents = [.. message.Components!];
        await Assert.That(topLevelComponents.Length).IsEqualTo(1);

        var container = (ComponentContainerProperties)topLevelComponents[0];
        IComponentContainerComponentProperties[] components = [.. container.Components!];
        await Assert.That(components.Length).IsEqualTo(5);

        var actionRow = (ActionRowProperties)components[2];
        IActionRowComponentProperties[] actionRowComponents = [.. actionRow.Components!];
        await Assert.That(actionRowComponents.Length).IsEqualTo(1);
        await Assert.That(actionRowComponents[0]).IsEqualTo(actions[0]);

        var separator = (ComponentSeparatorProperties)components[3];
        await Assert.That(separator.Divider).IsTrue();
        await Assert.That(separator.Spacing).IsEqualTo(ComponentSeparatorSpacingSize.Small);

        var footer = (TextDisplayProperties)components[4];
        await Assert.That(footer.Content).IsEqualTo(ExpectedFooter);
    }

    [Test]
    public async Task CreateCard_DefaultCard_UsesDefaultAccentColor()
    {
        var message = InteractionResponseBuilder.CreateCard(
            heading: "# Heading",
            body: "Body"
        );

        await Assert.That(message.Components).IsNotNull();
        IMessageComponentProperties[] topLevelComponents = [.. message.Components!];
        var container = (ComponentContainerProperties)topLevelComponents[0];

        await Assert.That(container.AccentColor).IsEqualTo(new Color(red: 35, green: 135, blue: 55));
    }

    [Test]
    public async Task CreateErrorCard_ErrorCard_UsesErrorAccentColor()
    {
        var message = InteractionResponseBuilder.CreateErrorCard(
            heading: "# Heading",
            body: "Body"
        );

        await Assert.That(message.Components).IsNotNull();
        IMessageComponentProperties[] topLevelComponents = [.. message.Components!];
        var container = (ComponentContainerProperties)topLevelComponents[0];

        await Assert.That(container.AccentColor).IsEqualTo(new Color(red: 250, green: 80, blue: 75));
    }

    [Test]
    public async Task ApplyCard_ModifyMessageOptions_ClearsContentAndSetsComponents()
    {
        var callback = InteractionCallback.ModifyMessage(static options =>
        {
            options.Content = "Existing content";

            InteractionResponseBuilder.ApplyCard(
                options: options,
                heading: "# Heading",
                body: "Body",
                flags: MessageFlags.Ephemeral
            );
        });

        var options = callback.Data;

        await Assert.That(options.Content).IsNull();
        await Assert.That(options.Flags).IsEqualTo(MessageFlags.IsComponentsV2 | MessageFlags.Ephemeral);
        await Assert.That(options.Components).IsNotNull();

        IMessageComponentProperties[] topLevelComponents = [.. options.Components!];
        await Assert.That(topLevelComponents.Length).IsEqualTo(1);

        var container = (ComponentContainerProperties)topLevelComponents[0];
        IComponentContainerComponentProperties[] components = [.. container.Components!];
        await Assert.That(components.Length).IsEqualTo(4);

        var heading = (TextDisplayProperties)components[0];
        await Assert.That(heading.Content).IsEqualTo("# Heading");

        var body = (TextDisplayProperties)components[1];
        await Assert.That(body.Content).IsEqualTo("Body");
    }
}