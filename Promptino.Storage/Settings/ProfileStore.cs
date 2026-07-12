using System.Text.Json;

namespace Promptino.Storage.Settings;

public sealed record SavedProfile(
    string Name,
    int Wpm,
    ReadingPreferences Preferences,
    bool AlwaysOnTop,
    bool ScreenShareSafeMode,
    string HotkeyGesture,
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    int SchemaVersion = 1);

public sealed class ProfileStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ProfileStore(string path)
    {
        _path = path;
    }

    public async Task<(IReadOnlyList<SavedProfile> Profiles, bool Recovered)> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return ([], false);
        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            var items = JsonSerializer.Deserialize(json, JsonStorageOptions.Context.ListSavedProfile);
            
            bool migrated = !json.Contains("SchemaVersion", StringComparison.OrdinalIgnoreCase);
            var result = items ?? new List<SavedProfile>();
            if (migrated && result.Count > 0)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    result[i] = result[i] with { SchemaVersion = 1 };
                }
                await SaveAllAsync(result, ct);
            }
            return (result, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileStore] LoadAsync error: {ex.Message}");
            return ([], true);
        }
    }

    public static SavedProfile CreateDefault() => new(
        "Default",
        Wpm: 130,
        Preferences: ReadingPreferences.Defaults,
        AlwaysOnTop: false,
        ScreenShareSafeMode: false,
        HotkeyGesture: "Ctrl+Alt+Space",
        WindowX: 100, WindowY: 100, WindowWidth: 900, WindowHeight: 560);

    public async Task<bool> EnsureDefaultProfileAsync(IReadOnlyList<SavedProfile> currentProfiles, CancellationToken ct = default)
    {
        if (currentProfiles.Count > 0) return false;
        return await SaveAllAsync([CreateDefault()], ct);
    }

    public async Task<bool> SaveAllAsync(IReadOnlyList<SavedProfile> profiles, CancellationToken ct = default)
    {
        var tempPath = _path + ".tmp";
        await _saveLock.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(profiles.ToList(), JsonStorageOptions.Context.ListSavedProfile);
            await IoRetry.RunAsync(async ct2 =>
            {
                await File.WriteAllTextAsync(tempPath, json, ct2);
                File.Move(tempPath, _path, overwrite: true);
            }, ct);
            return true;
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
        finally { _saveLock.Release(); }
    }

    public void Dispose() => _saveLock.Dispose();
}
