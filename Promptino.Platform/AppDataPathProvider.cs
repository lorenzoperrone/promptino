namespace Promptino.Platform;

/// <summary>
/// Provides resolution of standard application data paths (settings, logs, recent files).
/// </summary>
public interface IAppDataPathProvider
{
    /// <summary>Gets the full file path for local application settings JSON storage.</summary>
    string GetSettingsFilePath();

    /// <summary>Gets the full file path for application log output.</summary>
    string GetLogFilePath();

    /// <summary>Gets the full file path for recent files JSON storage.</summary>
    string GetRecentFilesFilePath();
}

/// <summary>
/// Windows platform implementation resolving paths under %APPDATA%\Promptino.
/// </summary>
public sealed class WindowsAppDataPathProvider : IAppDataPathProvider
{
    /// <inheritdoc />
    public string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "settings.json");
    }

    /// <inheritdoc />
    public string GetLogFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "logs", "promptino.log");
    }

    /// <inheritdoc />
    public string GetRecentFilesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "recent-files.json");
    }
}
