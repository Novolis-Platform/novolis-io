namespace Novolis.IO.Workspace;

/// <summary>Keys for keyed <see cref="IFileWorkspace"/> DI registrations.</summary>
public static class FileWorkspaceKeys
{
    public const string Storage = nameof(Storage);
    public const string JsonFileEvents = nameof(JsonFileEvents);
    public const string JsonFilesStore = nameof(JsonFilesStore);
}
