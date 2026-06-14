namespace GitHubWidgetBot;

internal static class GlobalConstants
{
    public static readonly string Version = typeof(GlobalConstants).Assembly.GetName().Version?.ToString(fieldCount: 3) ?? "Unknown";

    public const string WidgetSetupModalId = "setup-modal";
    public const string WidgetSetupAccountNameId = "github-acc-name";
    public const string WidgetSetupExcludeUnknownId = "github-exclude-unknown";
}