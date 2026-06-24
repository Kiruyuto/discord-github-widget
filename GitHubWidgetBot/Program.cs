using GitHubWidgetBot.Modules;
using GitHubWidgetBot.Persistence;
using GitHubWidgetBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
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
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        AddNetCord(builder.Services);
        AddGitHub(builder.Services);
        AddDatabase(builder.Services, builder.Configuration, builder.Environment);

        var host = RegisterCommandModules(builder.Build());

        await MigrateDatabaseAsync(host).ConfigureAwait(false);
        await host.RunAsync().ConfigureAwait(false);
    }

    private static async Task MigrateDatabaseAsync(IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static void AddNetCord(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddOptions<DiscordOptions>()
            .BindConfiguration(DiscordOptions.SectionName)
            .ValidateOnStart();

        serviceCollection.AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>();

        serviceCollection
            .AddDiscordShardedGateway(options =>
            {
                options.Intents = null;
                options.ConnectionProperties = ConnectionPropertiesProperties.Android;

                options.Presence = new PresenceProperties(UserStatusType.Online)
                {
                    Activities =
                    [
                        new UserActivityProperties(ApplicationConfiguration.Version, UserActivityType.Custom)
                        {
                            State = $"🔧 Build v{ApplicationConfiguration.Version}"
                        }
                    ]
                };
            })
            .AddApplicationCommands(options =>
            {
                options.DefaultContexts = [InteractionContextType.BotDMChannel, InteractionContextType.DMChannel];
                options.DefaultIntegrationTypes = [ApplicationIntegrationType.UserInstall];
                options.AutoRegisterCommands = true;
            })
            .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();
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

    private static void AddDatabase(IServiceCollection serviceCollection, ConfigurationManager configuration, IHostEnvironment environment)
    {
        var databaseConnectionString = configuration[ApplicationConfiguration.Database.ConnectionString];
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);

        serviceCollection.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString, builder => builder.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema: ApplicationConfiguration.Database.SchemaName));
            options.UseSnakeCaseNamingConvention();

            if (!environment.IsProduction())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
    }

    private static IHost RegisterCommandModules(IHost host)
    {
        host.AddApplicationCommandModule<ApplicationCommandsModule>();

        host.AddComponentInteractionModule<ButtonInteractionContext, ButtonModule>();
        host.AddComponentInteractionModule<ModalInteractionContext, ModalsModule>();

        return host;
    }
}