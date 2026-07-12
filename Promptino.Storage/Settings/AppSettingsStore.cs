using System.Text.Json;

namespace Promptino.Storage.Settings;

public enum ReadingGuideMode
{
    None = 0,
    Line = 1,
    HighlightBand = 2,
    Both = 3
}

public enum PromptinoTextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
    Justify = 3
}

public sealed record ReadingPreferences(
    int TextSize,
    double LineSpacing,
    double WindowOpacity,
    bool AlwaysOnTop,
    string FontFamily,
    int ReadingMargin,
    bool HorizontalMirror = false,
    string TextColor = "#F4F8FB",
    string BackgroundColor = "#141B22",
    ReadingGuideMode ReadingGuide = ReadingGuideMode.Both,
    PromptinoTextAlignment TextAlignment = PromptinoTextAlignment.Left)
{
    public const int MinTextSize = 2;
    public const int MaxTextSize = 96;
    public const double MinOpacity = 0.0;
    public const double MaxOpacity = 1.0;
    public const int MinReadingMargin = 0;
    public const int MaxReadingMargin = 120;

    public static ReadingPreferences Defaults => new(32, 1.4, 1.0, false, "Segoe UI", 40, false, "#F4F8FB", "#141B22", ReadingGuideMode.Both, PromptinoTextAlignment.Left);

    public ReadingPreferences Clamped() => this with
    {
        TextSize = Math.Clamp(TextSize, MinTextSize, MaxTextSize),
        LineSpacing = Math.Max(0.5, LineSpacing),
        WindowOpacity = Math.Clamp(WindowOpacity, MinOpacity, MaxOpacity),
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? Defaults.FontFamily : FontFamily,
        ReadingMargin = Math.Clamp(ReadingMargin, MinReadingMargin, MaxReadingMargin),
        TextColor = string.IsNullOrWhiteSpace(TextColor) ? Defaults.TextColor : TextColor,
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? Defaults.BackgroundColor : BackgroundColor,
        ReadingGuide = Enum.IsDefined(typeof(ReadingGuideMode), ReadingGuide) ? ReadingGuide : ReadingGuideMode.Both,
        TextAlignment = Enum.IsDefined(typeof(PromptinoTextAlignment), TextAlignment) ? TextAlignment : PromptinoTextAlignment.Left
    };
}

public enum PlaybackSmoothnessMode
{
    RenderAligned = 0,
    OversampledTimer = 1,
}

public enum PrompterScrollMode
{
    Basic = 0,
    HighPerformance = 1,
}

public sealed record GlobalHotkeySettings(bool Enabled, string Gesture, string? NextMarkerGesture = null, string? PrevMarkerGesture = null)
{
    public static GlobalHotkeySettings Defaults => new(true, "Ctrl+Alt+Space", "PageDown", "PageUp");
}

public sealed record WindowBoundsSettings(int X, int Y, int Width, int Height)
{
    public static WindowBoundsSettings DefaultRemote => new(120, 120, 260, 150);
}

public sealed record AppSettings(
    bool CalibrationCompleted,
    int DefaultWpm,
    ReadingPreferences Preferences,
    GlobalHotkeySettings? Hotkeys = null,
    WindowBoundsSettings? RemoteWindowBounds = null,
    PlaybackSmoothnessMode? PlaybackMode = null,
    PrompterScrollMode? ScrollMode = null,
    string? AppTheme = null,
    string? ExternalEditorPath = null,
    string? Language = null,
    int SchemaVersion = 1)
{
    public GlobalHotkeySettings HotkeySettings => Hotkeys ?? GlobalHotkeySettings.Defaults;
    public WindowBoundsSettings EffectiveRemoteWindowBounds => RemoteWindowBounds ?? WindowBoundsSettings.DefaultRemote;
    public PlaybackSmoothnessMode EffectivePlaybackMode => PlaybackMode ?? PlaybackSmoothnessMode.RenderAligned;
    public PrompterScrollMode EffectiveScrollMode => ScrollMode ?? PrompterScrollMode.HighPerformance;
    public string EffectiveAppTheme => AppTheme == "Dark" ? "Dark" : "Light";
    public string EffectiveLanguage => string.IsNullOrWhiteSpace(Language) ? "Auto" : Language;
    public static AppSettings Defaults => new(false, 130, ReadingPreferences.Defaults, GlobalHotkeySettings.Defaults);
}

public sealed record SettingsLoadResult(AppSettings Settings, bool Recovered, string? RecoveryReason)
{
    public bool IsGenuineFailure => Recovered && RecoveryReason != "missing";
}

public sealed class AppSettingsStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppSettingsStore(string path)
    {
        _path = path;
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return new SettingsLoadResult(AppSettings.Defaults, true, "missing");

        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            var parsed = JsonSerializer.Deserialize(json, JsonStorageOptions.Context.AppSettings);
            if (parsed is null) return new SettingsLoadResult(AppSettings.Defaults, true, "incompatible");

            bool migrated = !json.Contains("SchemaVersion", StringComparison.OrdinalIgnoreCase);
            if (migrated)
            {
                parsed = parsed with { SchemaVersion = 1 };
            }

            var safePrefs = (parsed.Preferences ?? ReadingPreferences.Defaults).Clamped();
            var safeMode = Enum.IsDefined(parsed.EffectivePlaybackMode)
                ? parsed.EffectivePlaybackMode
                : PlaybackSmoothnessMode.RenderAligned;
            var safeScrollMode = Enum.IsDefined(parsed.EffectiveScrollMode)
                ? parsed.EffectiveScrollMode
                : PrompterScrollMode.HighPerformance;
            var safeAppTheme = parsed.AppTheme == "Dark" ? "Dark" : "Light";
            var safeEditorPath = string.IsNullOrWhiteSpace(parsed.ExternalEditorPath) ? null : parsed.ExternalEditorPath.Trim();
            var normalized = parsed with { Preferences = safePrefs, Hotkeys = parsed.HotkeySettings, PlaybackMode = safeMode, ScrollMode = safeScrollMode, AppTheme = safeAppTheme, ExternalEditorPath = safeEditorPath };

            if (migrated)
            {
                await SaveAsync(normalized, ct);
            }
            return new SettingsLoadResult(normalized, false, null);
        }
        catch (JsonException) { return new SettingsLoadResult(AppSettings.Defaults, true, "corrupt"); }
        catch (IOException) { return new SettingsLoadResult(AppSettings.Defaults, true, "unreadable"); }
        catch (UnauthorizedAccessException) { return new SettingsLoadResult(AppSettings.Defaults, true, "unreadable"); }
    }

    public async Task<bool> SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var tempPath = _path + ".tmp";
        await _saveLock.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings with { Hotkeys = settings.HotkeySettings, ScrollMode = settings.EffectiveScrollMode }, JsonStorageOptions.Context.AppSettings);
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
