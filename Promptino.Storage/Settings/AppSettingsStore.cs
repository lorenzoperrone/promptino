using System.Text.Json;

namespace Promptino.Storage.Settings;

/// <summary>
/// Specifies reading guide visual highlight mode.
/// </summary>
public enum ReadingGuideMode
{
    /// <summary>No reading guide displayed.</summary>
    None = 0,
    /// <summary>Displays a horizontal line at the focus position.</summary>
    Line = 1,
    /// <summary>Highlights the active reading line with a semi-transparent band.</summary>
    HighlightBand = 2,
    /// <summary>Displays both focus line and highlight band.</summary>
    Both = 3
}

/// <summary>
/// Specifies script text alignment in the prompter view.
/// </summary>
public enum PromptinoTextAlignment
{
    /// <summary>Left-aligned text.</summary>
    Left = 0,
    /// <summary>Centered text.</summary>
    Center = 1,
    /// <summary>Right-aligned text.</summary>
    Right = 2,
    /// <summary>Justified text.</summary>
    Justify = 3
}

/// <summary>
/// Holds visual, typographic, and layout preferences for prompter rendering.
/// </summary>
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
    /// <summary>Minimum font size in points.</summary>
    public const int MinTextSize = 2;
    /// <summary>Maximum font size in points.</summary>
    public const int MaxTextSize = 96;
    /// <summary>Minimum window opacity (transparent).</summary>
    public const double MinOpacity = 0.0;
    /// <summary>Maximum window opacity (opaque).</summary>
    public const double MaxOpacity = 1.0;
    /// <summary>Minimum reading margin in pixels.</summary>
    public const int MinReadingMargin = 0;
    /// <summary>Maximum reading margin in pixels.</summary>
    public const int MaxReadingMargin = 120;

    /// <summary>Gets default reading preferences.</summary>
    public static ReadingPreferences Defaults => new(32, 1.4, 1.0, false, "Segoe UI", 40, false, "#F4F8FB", "#141B22", ReadingGuideMode.Both, PromptinoTextAlignment.Left);

    /// <summary>Returns a copy of preferences with all properties clamped within valid ranges.</summary>
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

/// <summary>
/// Specifies playback timer loop mode.
/// </summary>
public enum PlaybackSmoothnessMode
{
    /// <summary>Timer aligned with display frame rendering.</summary>
    RenderAligned = 0,
    /// <summary>Oversampled high-frequency timer loop.</summary>
    OversampledTimer = 1,
}

/// <summary>
/// Specifies prompter scroll interpolation mode.
/// </summary>
public enum PrompterScrollMode
{
    /// <summary>Standard scroll mode.</summary>
    Basic = 0,
    /// <summary>GPU-accelerated high performance smooth scroll mode.</summary>
    HighPerformance = 1,
}

/// <summary>
/// Holds global hotkey binding settings.
/// </summary>
public sealed record GlobalHotkeySettings(bool Enabled, string Gesture, string? NextMarkerGesture = null, string? PrevMarkerGesture = null)
{
    /// <summary>Gets default global hotkey bindings.</summary>
    public static GlobalHotkeySettings Defaults => new(true, "Ctrl+Alt+Space", "PageDown", "PageUp");
}

/// <summary>
/// Holds saved window coordinates and dimensions.
/// </summary>
public sealed record WindowBoundsSettings(int X, int Y, int Width, int Height)
{
    /// <summary>Gets default remote mini window position and dimensions.</summary>
    public static WindowBoundsSettings DefaultRemote => new(120, 120, 260, 150);
}

/// <summary>
/// Master application settings model serialized to <c>settings.json</c>.
/// </summary>
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
    bool? ClickThroughEnabled = null,
    int? TargetScreenIndex = null,
    bool? PrompterFullscreen = null,
    int? TargetPresentationMinutes = null,
    bool? ShowPresentationTimer = null,
    int SchemaVersion = 1)
{
    /// <summary>Gets active hotkey settings or defaults.</summary>
    public GlobalHotkeySettings HotkeySettings => Hotkeys ?? GlobalHotkeySettings.Defaults;
    /// <summary>Gets effective remote window bounds or defaults.</summary>
    public WindowBoundsSettings EffectiveRemoteWindowBounds => RemoteWindowBounds ?? WindowBoundsSettings.DefaultRemote;
    /// <summary>Gets effective playback smoothness mode.</summary>
    public PlaybackSmoothnessMode EffectivePlaybackMode => PlaybackMode ?? PlaybackSmoothnessMode.RenderAligned;
    /// <summary>Gets effective prompter scroll mode.</summary>
    public PrompterScrollMode EffectiveScrollMode => ScrollMode ?? PrompterScrollMode.HighPerformance;
    /// <summary>Gets active UI application theme ("Light" or "Dark").</summary>
    public string EffectiveAppTheme => AppTheme == "Dark" ? "Dark" : "Light";
    /// <summary>Gets active application language code.</summary>
    public string EffectiveLanguage => string.IsNullOrWhiteSpace(Language) ? "Auto" : Language;
    /// <summary>Gets a value indicating whether click-through mode is enabled.</summary>
    public bool EffectiveClickThroughEnabled => ClickThroughEnabled ?? false;
    /// <summary>Gets target monitor index for prompter window.</summary>
    public int EffectiveTargetScreenIndex => Math.Max(0, TargetScreenIndex ?? 0);
    /// <summary>Gets a value indicating whether prompter is displayed in fullscreen mode.</summary>
    public bool EffectivePrompterFullscreen => PrompterFullscreen ?? false;
    /// <summary>Gets target presentation timer duration in minutes.</summary>
    public int EffectiveTargetPresentationMinutes => Math.Max(0, TargetPresentationMinutes ?? 0);
    /// <summary>Gets a value indicating whether presentation timer widget is visible.</summary>
    public bool EffectiveShowPresentationTimer => ShowPresentationTimer ?? false;
    /// <summary>Gets default app settings snapshot.</summary>
    public static AppSettings Defaults => new(false, 130, ReadingPreferences.Defaults, GlobalHotkeySettings.Defaults);
}

/// <summary>
/// Result snapshot returned when loading settings from JSON.
/// </summary>
public sealed record SettingsLoadResult(AppSettings Settings, bool Recovered, string? RecoveryReason)
{
    /// <summary>Gets a value indicating whether load encountered a genuine parse error or file corruption.</summary>
    public bool IsGenuineFailure => Recovered && RecoveryReason != "missing";
}

/// <summary>
/// Manages JSON storage and atomic persistence for application settings (<c>settings.json</c>).
/// </summary>
public sealed class AppSettingsStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of <see cref="AppSettingsStore"/> with the target file path.
    /// </summary>
    public AppSettingsStore(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Loads application settings asynchronously, executing schema migration or recovery if corrupt.
    /// </summary>
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

    /// <summary>
    /// Saves settings asynchronously using atomic temporary file creation and replacement.
    /// </summary>
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
                await IoRetry.WriteTextWriteThroughAsync(tempPath, json, ct2);
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

    /// <inheritdoc />
    public void Dispose() => _saveLock.Dispose();
}
