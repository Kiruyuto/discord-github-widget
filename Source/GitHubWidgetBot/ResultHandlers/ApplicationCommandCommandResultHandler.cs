using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace GitHubWidgetBot.ResultHandlers;

internal sealed class ApplicationCommandCommandResultHandler<TContext> : IApplicationCommandResultHandler<TContext> where TContext : IApplicationCommandContext
{
    public async ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
    {
        if (result is not IFailResult failResult) return;

        var resultMessage = failResult.Message;
        var interaction = context.Interaction;

        if (failResult is IExceptionResult exceptionResult && logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(exceptionResult.Exception, "Execution of an application command of name '{Name}' failed with an exception", interaction.Data.Name);
        }
        else if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Execution of an application command of name '{Name}' failed with '{Message}'", interaction.Data.Name, resultMessage);
        }

        // Try the normal interaction response first.
        // If the interaction was already acknowledged by a handler (E.g.: Through deferral), edit the response instead.
        try
        {
            InteractionMessageProperties messageProps = new() { Content = ApplicationConfiguration.UserError, Flags = MessageFlags.Ephemeral, };
            await interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
        }
        catch (RestException ex) when (ex.Error?.Message == "Interaction has already been acknowledged.")
        {
            await interaction.ModifyResponseAsync(x => { x.Content = ApplicationConfiguration.UserError; });
        }
    }
}