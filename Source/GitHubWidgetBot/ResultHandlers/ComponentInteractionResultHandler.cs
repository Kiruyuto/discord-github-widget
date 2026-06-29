using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot.ResultHandlers;

internal sealed class ComponentInteractionResultHandler<TContext> : IComponentInteractionResultHandler<TContext> where TContext : IComponentInteractionContext
{
    public async ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
    {
        if (result is not IFailResult failResult) return;

        var resultMessage = failResult.Message;
        var interaction = context.Interaction;

        if (failResult is IExceptionResult exceptionResult && logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(exceptionResult.Exception, "Execution of an interaction of custom ID '{Id}' failed with an exception", interaction.Data.CustomId);
        }
        else if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Execution of an interaction of custom ID '{Id}' failed with '{Message}'", interaction.Data.CustomId, resultMessage);
        }

        // Try the normal interaction response first.
        // If the interaction was already acknowledged by a handler (E.g.: Through deferral), edit the response instead.
        try
        {
            var messageProps = InteractionResponseBuilder.CreateErrorCard(
                heading: "# Interaction failed",
                body: ApplicationConfiguration.UserError,
                flags: MessageFlags.Ephemeral
            );
            await interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
        }
        catch (RestException ex) when (ex.Error?.Message == "Interaction has already been acknowledged.")
        {
            await interaction.ModifyResponseAsync(static options => InteractionResponseBuilder.ApplyErrorCard(
                options: options,
                heading: "# Interaction failed",
                body: ApplicationConfiguration.UserError,
                flags: MessageFlags.Ephemeral
            ));
        }
    }
}