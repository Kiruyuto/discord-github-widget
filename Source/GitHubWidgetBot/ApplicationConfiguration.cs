namespace GitHubWidgetBot;

internal static class ApplicationConfiguration
{
    public static readonly string Version = typeof(ApplicationConfiguration).Assembly.GetName().Version?.ToString(fieldCount: 3) ?? "Unknown";
    public static readonly string UserAgent = $"discord-github-widget/{Version}";

    internal static class Database
    {
        public const string ConnectionString = "Database:ConnectionString";
        public const string SchemaName = "github_widget";
    }

    internal static class DiscordComponents
    {
        public const string WidgetSetupModalId = "setup-modal";
        public const string WidgetSetupManualModalId = "setup-manual-modal";
        public const string WidgetSetupVerifyButtonId = "verify-button";
        public const string WidgetSetupExcludeUnknownId = "github-exclude-unknown";
        public const string WidgetSetupManualGitHubUsernameId = "github-username";
    }
}