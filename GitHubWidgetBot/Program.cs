using GitHubWidgetBot.Modules;
using GitHubWidgetBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace GitHubWidgetBot;

internal static class Program
{
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods")]
    private static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        AddNetCord(builder.Services);
        AddGitHub(builder.Services);

        var host = RegisterCommandModules(builder.Build());

        return host.RunAsync();
    }

    private static void AddNetCord(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddDiscordShardedGateway(options =>
            {
                options.Intents = null;
                options.ConnectionProperties = ConnectionPropertiesProperties.Android;

                options.Presence = new PresenceProperties(UserStatusType.Online)
                {
                    Activities = [new UserActivityProperties(GlobalConstants.Version, UserActivityType.Custom) { State = $"🔧 Build v{GlobalConstants.Version}" }]
                };
            })
            .AddApplicationCommands(options =>
            {
                options.DefaultContexts = [InteractionContextType.BotDMChannel, InteractionContextType.Guild, InteractionContextType.DMChannel];
                options.DefaultIntegrationTypes = [ApplicationIntegrationType.UserInstall];
                options.AutoRegisterCommands = true;
            })
            .AddComponentInteractions<ModalInteraction, ModalInteractionContext>();
    }

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded")]
    private static void AddGitHub(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddOptions<GitHubOptions>()
            .BindConfiguration(GitHubOptions.SectionName)
            .ValidateOnStart();

        serviceCollection.AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>();
        serviceCollection.AddSingleton(static _ => new HttpClient { BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute) });
        serviceCollection.AddSingleton<GitHubService>();
    }

    private static IHost RegisterCommandModules(IHost host)
    {
        host.AddApplicationCommandModule<SetupModule>();

        host.AddComponentInteractionModule<ModalModule>();

        return host;
    }
}