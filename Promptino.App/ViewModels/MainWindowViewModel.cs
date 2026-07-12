using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Promptino.App.Services;
using Promptino.Core.Playback;
using Promptino.Core.Scripts;
using Promptino.Platform;
using Promptino.Storage.Settings;

namespace Promptino.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ScriptLoaderService _scriptLoader;
    private readonly AppSettingsStore _settingsStore;
    private readonly IExternalEditorService _externalEditor;
    private readonly ILogger _logger;
    private readonly RecentFilesStore _recentFilesStore;
    private bool _calibrationCompleted;

    public MainWindowViewModel()
        : this(new ScriptLoaderService(new LocalScriptFileReader(), new ScriptTextTransformer()),
               new AppSettingsStore(new WindowsAppDataPathProvider().GetSettingsFilePath()),
               new ExternalEditorService(),
               new FileLogger(),
               new RecentFilesStore(new WindowsAppDataPathProvider().GetRecentFilesFilePath())) { }

    public MainWindowViewModel(
        ScriptLoaderService scriptLoader,
        AppSettingsStore settingsStore,
        IExternalEditorService? externalEditorService = null,
        ILogger? logger = null,
        RecentFilesStore? recentFilesStore = null)
    {
        _scriptLoader = scriptLoader;
        _settingsStore = settingsStore;
        _externalEditor = externalEditorService ?? new ExternalEditorService();
        _logger = logger ?? new FileLogger();
        _recentFilesStore = recentFilesStore ?? new RecentFilesStore(new WindowsAppDataPathProvider().GetRecentFilesFilePath());
        _statusMessage = Localizer.Get("MsgNoScriptLoaded");
    }

    public ILogger Logger => _logger;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); } }

    public static IReadOnlyList<string> PrimaryActions { get; } = ["Load Script", "Play/Pause", "Adjust Speed", "Display Settings"];
    public static IReadOnlyList<string> ForbiddenTerms { get; } = ["AI", "Microphone", "Transcription", "Cloud", "Login", "Account", "Telemetry", "Editor Suite"];

    private string _statusMessage = "No script loaded.";
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }
    private string? _settingsRecoveryMessage;
    public string? SettingsRecoveryMessage { get => _settingsRecoveryMessage; private set => Set(ref _settingsRecoveryMessage, value); }
    private string? _settingsSaveFailureMessage;
    public string? SettingsSaveFailureMessage { get => _settingsSaveFailureMessage; private set => Set(ref _settingsSaveFailureMessage, value); }
    private ScriptDocument? _loadedScript;
    public ScriptDocument? LoadedScript { get => _loadedScript; private set => Set(ref _loadedScript, value); }

    private bool _isCalibrationRecommended;
    public bool IsCalibrationRecommended { get => _isCalibrationRecommended; private set => Set(ref _isCalibrationRecommended, value); }
    private bool _isCalibrationVisible;
    public bool IsCalibrationVisible { get => _isCalibrationVisible; private set => Set(ref _isCalibrationVisible, value); }
    private int _calibrationWpm = ReadingSpeed.DefaultWpm;
    public int CalibrationWpm { get => _calibrationWpm; private set => Set(ref _calibrationWpm, value); }

    private int _textSize = ReadingPreferences.Defaults.TextSize;
    public int TextSize { get => _textSize; private set => Set(ref _textSize, value); }
    private double _lineSpacing = ReadingPreferences.Defaults.LineSpacing;
    public double LineSpacing { get => _lineSpacing; private set => Set(ref _lineSpacing, value); }
    private double _windowOpacity = ReadingPreferences.Defaults.WindowOpacity;
    public double WindowOpacity { get => _windowOpacity; private set => Set(ref _windowOpacity, value); }
    private bool _alwaysOnTop = ReadingPreferences.Defaults.AlwaysOnTop;
    public bool AlwaysOnTop { get => _alwaysOnTop; private set => Set(ref _alwaysOnTop, value); }
    private string _fontFamily = ReadingPreferences.Defaults.FontFamily;
    public string FontFamily { get => _fontFamily; private set => Set(ref _fontFamily, value); }
    private int _readingMargin = ReadingPreferences.Defaults.ReadingMargin;
    public int ReadingMargin { get => _readingMargin; private set => Set(ref _readingMargin, value); }
    private bool _horizontalMirror = ReadingPreferences.Defaults.HorizontalMirror;
    public bool HorizontalMirror { get => _horizontalMirror; private set => Set(ref _horizontalMirror, value); }
    private string _textColor = ReadingPreferences.Defaults.TextColor;
    public string TextColor { get => _textColor; private set => Set(ref _textColor, value); }
    private string _backgroundColor = ReadingPreferences.Defaults.BackgroundColor;
    public string BackgroundColor { get => _backgroundColor; private set => Set(ref _backgroundColor, value); }
    private ReadingGuideMode _readingGuide = ReadingPreferences.Defaults.ReadingGuide;
    public ReadingGuideMode ReadingGuide { get => _readingGuide; private set => Set(ref _readingGuide, value); }
    private PromptinoTextAlignment _textAlignment = ReadingPreferences.Defaults.TextAlignment;
    public PromptinoTextAlignment TextAlignment { get => _textAlignment; private set => Set(ref _textAlignment, value); }

    private string? _windowBehaviorWarning;
    public string? WindowBehaviorWarning { get => _windowBehaviorWarning; private set => Set(ref _windowBehaviorWarning, value); }
    private bool _screenShareSafeModeEnabled;
    public bool ScreenShareSafeModeEnabled { get => _screenShareSafeModeEnabled; private set => Set(ref _screenShareSafeModeEnabled, value); }

    private string _hotkeyGesture = GlobalHotkeySettings.Defaults.Gesture;
    public string HotkeyGesture { get => _hotkeyGesture; private set => Set(ref _hotkeyGesture, value); }
    private string _nextMarkerGesture = GlobalHotkeySettings.Defaults.NextMarkerGesture ?? "PageDown";
    public string NextMarkerGesture { get => _nextMarkerGesture; private set => Set(ref _nextMarkerGesture, value); }
    private string _prevMarkerGesture = GlobalHotkeySettings.Defaults.PrevMarkerGesture ?? "PageUp";
    public string PrevMarkerGesture { get => _prevMarkerGesture; private set => Set(ref _prevMarkerGesture, value); }
    private string? _profilesWarningMessage;
    public string? ProfilesWarningMessage { get => _profilesWarningMessage; private set => Set(ref _profilesWarningMessage, value); }
    private PlaybackSmoothnessMode _playbackSmoothnessMode = PlaybackSmoothnessMode.RenderAligned;
    public PlaybackSmoothnessMode PlaybackSmoothnessMode { get => _playbackSmoothnessMode; private set => Set(ref _playbackSmoothnessMode, value); }
    private PrompterScrollMode _prompterScrollMode = PrompterScrollMode.HighPerformance;
    public PrompterScrollMode PrompterScrollMode { get => _prompterScrollMode; private set => Set(ref _prompterScrollMode, value); }
    private string _appTheme = "Light";
    public string AppTheme { get => _appTheme; private set => Set(ref _appTheme, value); }
    private string _externalEditorPath = string.Empty;
    public string ExternalEditorPath { get => _externalEditorPath; private set => Set(ref _externalEditorPath, value); }
    private string _language = "Auto";
    public string Language { get => _language; private set => Set(ref _language, value); }
    public bool HighPerformanceTextScrollingEnabled => PrompterScrollMode == PrompterScrollMode.HighPerformance;
    private ObservableCollection<ScriptMarker> _scriptMarkers = new();
    public ObservableCollection<ScriptMarker> ScriptMarkers { get => _scriptMarkers; private set => Set(ref _scriptMarkers, value); }
    private ObservableCollection<RecentFileEntry> _recentFiles = new();
    public ObservableCollection<RecentFileEntry> RecentFiles { get => _recentFiles; private set => Set(ref _recentFiles, value); }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Initializing MainWindowViewModel and loading settings.");
        var load = await _settingsStore.LoadAsync(cancellationToken);
        var settings = load.Settings;
        if (load.IsGenuineFailure)
        {
            _logger.LogWarning("Local settings were unavailable, loaded safe defaults.");
            SettingsRecoveryMessage = Localizer.Get("MsgSettingsRecovery");
        }
        else
        {
            _logger.LogInfo("Settings loaded successfully.");
            SettingsRecoveryMessage = null;
        }

        var recentLoad = await _recentFilesStore.LoadAsync(cancellationToken);
        RecentFiles = new ObservableCollection<RecentFileEntry>(recentLoad.Entries);

        _calibrationCompleted = settings.CalibrationCompleted;
        CalibrationWpm = ReadingSpeed.Clamp(settings.DefaultWpm);
        IsCalibrationRecommended = !settings.CalibrationCompleted;
        IsCalibrationVisible = !settings.CalibrationCompleted;

        var p = settings.Preferences;
        TextSize = p.TextSize; LineSpacing = p.LineSpacing; WindowOpacity = p.WindowOpacity; AlwaysOnTop = p.AlwaysOnTop; FontFamily = p.FontFamily; ReadingMargin = p.ReadingMargin; HorizontalMirror = p.HorizontalMirror; TextColor = p.TextColor; BackgroundColor = p.BackgroundColor; ReadingGuide = p.ReadingGuide; TextAlignment = p.TextAlignment;
        HotkeyGesture = settings.HotkeySettings.Gesture;
        NextMarkerGesture = settings.HotkeySettings.NextMarkerGesture ?? "PageDown";
        PrevMarkerGesture = settings.HotkeySettings.PrevMarkerGesture ?? "PageUp";
        PlaybackSmoothnessMode = settings.EffectivePlaybackMode;
        PrompterScrollMode = settings.EffectiveScrollMode;
        AppTheme = settings.EffectiveAppTheme;
        ExternalEditorPath = settings.ExternalEditorPath ?? string.Empty;
        Language = settings.EffectiveLanguage;
    }

    public bool TrySetHotkeyGesture(string value, out string? warning)
    {
        warning = ParseHotkey(value, out _) ? null : "Shortcut non valido. Usa almeno un modificatore (Ctrl/Alt/Shift/Win) e un tasto finale.";
        if (warning is null) HotkeyGesture = NormalizeGesture(value);
        return warning is null;
    }

    public bool TrySetNextMarkerGesture(string value, out string? warning)
    {
        warning = ParseHotkey(value, out _) ? null : "Shortcut non valido.";
        if (warning is null) NextMarkerGesture = NormalizeGesture(value);
        return warning is null;
    }

    public bool TrySetPrevMarkerGesture(string value, out string? warning)
    {
        warning = ParseHotkey(value, out _) ? null : "Shortcut non valido.";
        if (warning is null) PrevMarkerGesture = NormalizeGesture(value);
        return warning is null;
    }

    public bool TryGetParsedHotkey(out GlobalHotkey hotkey) => ParseHotkey(HotkeyGesture, out hotkey);
    public bool TryGetParsedNextMarkerHotkey(out GlobalHotkey hotkey) => ParseHotkey(NextMarkerGesture, out hotkey);
    public bool TryGetParsedPrevMarkerHotkey(out GlobalHotkey hotkey) => ParseHotkey(PrevMarkerGesture, out hotkey);

    private static string NormalizeGesture(string value) => string.Join('+', value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool ParseHotkey(string gesture, out GlobalHotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(gesture)) return false;
        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        
        HotkeyModifiers mod = HotkeyModifiers.None;
        if (parts.Length > 1)
        {
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var p = parts[i].ToLowerInvariant();
                mod |= p switch
                {
                    "ctrl" or "control" => HotkeyModifiers.Control,
                    "alt" => HotkeyModifiers.Alt,
                    "shift" => HotkeyModifiers.Shift,
                    "win" or "windows" => HotkeyModifiers.Win,
                    _ => HotkeyModifiers.None
                };
            }
        }
        
        var key = parts[^1].ToUpperInvariant();
        int vk = key switch
        {
            "SPACE" => 0x20,
            "PAGEDOWN" => 0x22,
            "PAGEUP" => 0x21,
            _ when key.Length == 1 && char.IsLetterOrDigit(key[0]) => key[0],
            _ => 0
        };
        if (vk == 0) return false;
        hotkey = new GlobalHotkey(mod, vk);
        return hotkey.IsValid;
    }

    public async Task SavePreferencesAsync(CancellationToken cancellationToken = default)
    {
        var prefs = new ReadingPreferences(TextSize, LineSpacing, WindowOpacity, AlwaysOnTop, FontFamily, ReadingMargin, HorizontalMirror, TextColor, BackgroundColor, ReadingGuide, TextAlignment);
        var saved = await _settingsStore.SaveAsync(new AppSettings(
            _calibrationCompleted,
            CalibrationWpm,
            prefs,
            new GlobalHotkeySettings(true, HotkeyGesture, NextMarkerGesture, PrevMarkerGesture),
            PlaybackMode: PlaybackSmoothnessMode,
            ScrollMode: PrompterScrollMode,
            AppTheme: AppTheme,
            ExternalEditorPath: string.IsNullOrWhiteSpace(ExternalEditorPath) ? null : ExternalEditorPath.Trim(),
            Language: Language), cancellationToken);
        if (saved)
        {
            _logger.LogInfo("Preferences saved successfully.");
            SettingsSaveFailureMessage = null;
        }
        else
        {
            _logger.LogWarning("Could not save preferences locally.");
            SettingsSaveFailureMessage = Localizer.Get("MsgSettingsSaveFailure");
        }
    }

    public async Task ConfirmCalibrationAsync(CancellationToken cancellationToken = default)
    {
        _calibrationCompleted = true;
        await SavePreferencesAsync(cancellationToken);
        IsCalibrationRecommended = false;
        IsCalibrationVisible = false;
        StatusMessage = Localizer.Get("MsgDefaultSpeedSaved", CalibrationWpm);
    }

    public void SkipCalibration() { IsCalibrationVisible = false; StatusMessage = Localizer.Get("MsgCalibrationSkipped"); }
    public void OpenCalibration() { IsCalibrationVisible = true; IsCalibrationRecommended = true; }
    public void SetCalibrationWpm(int wpm) => CalibrationWpm = ReadingSpeed.Clamp(wpm);
    public void SetPersonalization(int textSize, double lineSpacing, double windowOpacity, bool alwaysOnTop, string fontFamily, int readingMargin, bool horizontalMirror = false, string textColor = "#F4F8FB", string backgroundColor = "#141B22", ReadingGuideMode readingGuide = ReadingGuideMode.Both, PromptinoTextAlignment textAlignment = PromptinoTextAlignment.Left)
    {
        var clamped = new ReadingPreferences(textSize, lineSpacing, windowOpacity, alwaysOnTop, fontFamily, readingMargin, horizontalMirror, textColor, backgroundColor, readingGuide, textAlignment).Clamped();
        TextSize = clamped.TextSize; LineSpacing = clamped.LineSpacing; WindowOpacity = clamped.WindowOpacity; AlwaysOnTop = clamped.AlwaysOnTop; FontFamily = clamped.FontFamily; ReadingMargin = clamped.ReadingMargin; HorizontalMirror = clamped.HorizontalMirror; TextColor = clamped.TextColor; BackgroundColor = clamped.BackgroundColor; ReadingGuide = clamped.ReadingGuide; TextAlignment = clamped.TextAlignment;
    }
    public void SetColors(string textColor, string backgroundColor)
    {
        TextColor = string.IsNullOrWhiteSpace(textColor) ? ReadingPreferences.Defaults.TextColor : textColor;
        BackgroundColor = string.IsNullOrWhiteSpace(backgroundColor) ? ReadingPreferences.Defaults.BackgroundColor : backgroundColor;
    }
    public void SetAlwaysOnTop(bool value) => AlwaysOnTop = value;
    public void SetTextSize(int value) => TextSize = Math.Clamp(value, ReadingPreferences.MinTextSize, ReadingPreferences.MaxTextSize);
    public void SetWindowOpacity(double value) => WindowOpacity = Math.Clamp(value, ReadingPreferences.MinOpacity, ReadingPreferences.MaxOpacity);
    public void SetReadingMargin(int value) => ReadingMargin = Math.Clamp(value, ReadingPreferences.MinReadingMargin, ReadingPreferences.MaxReadingMargin);
    public void SetHorizontalMirror(bool value) => HorizontalMirror = value;
    public void SetReadingGuideMode(ReadingGuideMode mode) => ReadingGuide = mode;
    public void SetTextAlignment(PromptinoTextAlignment alignment) => TextAlignment = alignment;
    public void SetPlaybackSmoothnessMode(PlaybackSmoothnessMode mode) => PlaybackSmoothnessMode = mode;
    public void SetPrompterScrollMode(PrompterScrollMode mode)
    {
        PrompterScrollMode = mode;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighPerformanceTextScrollingEnabled)));
    }
    public void SetWindowBehaviorWarning(string? message) => WindowBehaviorWarning = message;
    public void SetProfilesWarning(string? message) => ProfilesWarningMessage = message;
    public void SetScreenShareSafeModeEnabled(bool value) => ScreenShareSafeModeEnabled = value;
    public void SetAppTheme(string theme) => AppTheme = theme == "Dark" ? "Dark" : "Light";
    public void SetExternalEditorPath(string? value) => ExternalEditorPath = value?.Trim() ?? string.Empty;
    public void NotifyAlwaysOnTopApplyFailure() => WindowBehaviorWarning = Localizer.Get("WarnAlwaysOnTopApplyFailure");

    public bool TryOpenLoadedScriptInExternalEditor(out string warning)
    {
        if (LoadedScript is null)
        {
            warning = Localizer.Get("WarnLoadScriptFirstEdit");
            return false;
        }

        var ok = _externalEditor.TryOpenScript(LoadedScript.SourcePath, ExternalEditorPath, out warning);
        if (ok)
        {
            _logger.LogInfo("Opened script in external editor.");
            StatusMessage = Localizer.Get("MsgOpenedExternalEditor", LoadedScript.SourcePath);
        }
        else
        {
            _logger.LogWarning($"Failed to open script in external editor. Warning: {warning}");
            StatusMessage = Localizer.Get("MsgFailedExternalEditor");
        }
        return ok;
    }

    public async Task LoadScriptAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Loading script file.");
        var result = await _scriptLoader.LoadAsync(path, cancellationToken);
        if (result.Success)
        {
            LoadedScript = result.Document;
            ErrorMessage = null;
            StatusMessage = Localizer.Get("MsgLoadedScript", path);
            SetMarkers(result.Document!.Markers ?? Array.Empty<ScriptMarker>());
            _logger.LogInfo("Script loaded successfully.");

            await _recentFilesStore.AddFileAsync(path, cancellationToken);
            var recentLoad = await _recentFilesStore.LoadAsync(cancellationToken);
            RecentFiles = new ObservableCollection<RecentFileEntry>(recentLoad.Entries);
            return;
        }
        LoadedScript = null;
        ErrorMessage = result.ErrorMessage;
        StatusMessage = Localizer.Get("MsgNoScriptLoaded");
        SetMarkers(Array.Empty<ScriptMarker>());
        _logger.LogWarning($"Failed to load script. Error: {result.ErrorMessage}");
    }

    public async Task<bool> ReloadScriptAsync(CancellationToken cancellationToken = default)
    {
        if (LoadedScript is null) return false;
        _logger.LogInfo("Reloading script file.");
        var result = await _scriptLoader.LoadAsync(LoadedScript.SourcePath, cancellationToken);
        if (result.Success)
        {
            LoadedScript = result.Document;
            ErrorMessage = null;
            WindowBehaviorWarning = null;
            StatusMessage = Localizer.Get("MsgAutoReloaded", LoadedScript!.SourcePath);
            SetMarkers(result.Document!.Markers ?? Array.Empty<ScriptMarker>());
            _logger.LogInfo("Script reloaded successfully.");
            return true;
        }
        ErrorMessage = result.ErrorMessage;
        WindowBehaviorWarning = result.ErrorMessage;
        _logger.LogWarning($"Failed to reload script. Error: {result.ErrorMessage}");
        return false;
    }

    public void SetMarkers(IEnumerable<ScriptMarker> markers)
    {
        var ordered = markers.OrderBy(m => m.Order).ToList();
        _scriptMarkers.Clear();
        foreach (var m in ordered) _scriptMarkers.Add(m);
    }

    public void AddMarker(string label, double ratio)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var marker = new ScriptMarker(id, label, ScriptMarkers.Count + 1, ratio);
        var list = ScriptMarkers.ToList();
        list.Add(marker);
        SetMarkers(list);
    }

    public void RemoveMarker(string id)
    {
        var list = ScriptMarkers.Where(m => m.Id != id).ToList();
        SetMarkers(list);
    }

    public void UpdateMarker(string id, string label)
    {
        var list = ScriptMarkers.Select(m => m.Id == id ? m with { Label = label } : m).ToList();
        SetMarkers(list);
    }

    public void SetLanguage(string lang) => Language = lang;

    public void RefreshLocalizedStrings()
    {
        if (LoadedScript is null)
        {
            StatusMessage = Localizer.Get("MsgNoScriptLoaded");
        }
        else
        {
            StatusMessage = Localizer.Get("MsgLoadedScript", LoadedScript.SourcePath);
        }

        if (SettingsRecoveryMessage != null)
        {
            SettingsRecoveryMessage = Localizer.Get("MsgSettingsRecovery");
        }

        if (SettingsSaveFailureMessage != null)
        {
            SettingsSaveFailureMessage = Localizer.Get("MsgSettingsSaveFailure");
        }
    }

    public void Dispose()
    {
        (_settingsStore as IDisposable)?.Dispose();
        (_recentFilesStore as IDisposable)?.Dispose();
    }
}
