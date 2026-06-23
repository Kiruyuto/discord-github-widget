namespace GitHubWidgetBot.Persistence.DTOs;

internal abstract class DbEntity
{
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "False-positive")]
    public int Id { get; private set; }
}