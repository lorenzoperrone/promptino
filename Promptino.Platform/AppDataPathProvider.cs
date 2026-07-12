namespace Promptino.Platform;

public interface IAppDataPathProvider
{
    string GetSettingsFilePath();
    string GetLogFilePath();
    string GetRecentFilesFilePath();
}

public sealed class WindowsAppDataPathProvider : IAppDataPathProvider
{
    public string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "settings.json");
    }

    public string GetLogFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "logs", "promptino.log");
    }

    public string GetRecentFilesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Promptino", "recent-files.json");
    }
}
