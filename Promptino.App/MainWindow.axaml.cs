using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Promptino.App.ViewModels;
using Promptino.Core.Playback;
using Promptino.Core.Scripts;
using Promptino.Platform;
using Promptino.Storage.Settings;
using Promptino.App.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Promptino.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowPriorityService _windowPriorityService;
    private readonly DispatcherTimer _playbackTimer;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ProfileCoordinator _profileCoordinator;
    private readonly RemoteWindowBoundsService _remoteBoundsService;
    private readonly IScriptWatcher _scriptWatcher;
    private PrompterWindow? _prompterWindow;
    private RemoteMiniWindow? _remoteWindow;
    private PlaybackSession? _playbackSession;
    private long _lastTickTimestamp;
    private PixelRect? _lastPrompterBounds;
    private PixelRect? _lastRemoteBounds;
    private bool _cleanupPreviewActive;
    private string? _cleanedPreviewScriptText;
    private string _activeScriptText = string.Empty;
    private string _displayScriptText = string.Empty;
    private bool _compositionPlaybackActive;
    private bool _compositionFrameScheduled;
    private string _playbackDriverStatus = "Active driver: idle";
    private bool _isShuttingDown;
    private long _lastEditScriptClickTimestamp;
    private TextBox? _recordingTarget;
    private Button? _recordingButton;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _playbackDriverStatus = Localizer.Get("DriverStatus", Localizer.Get("DriverIdle"));
        _viewModel.Logger.LogInfo("MainWindow initializing.");
        _windowPriorityService = new WindowPriorityService();
        var settingsPath = new WindowsAppDataPathProvider().GetSettingsFilePath();
        var profileStore = new ProfileStore(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath) ?? ".", "profiles.json"));
        _profileCoordinator = new ProfileCoordinator(profileStore);
        _remoteBoundsService = new RemoteWindowBoundsService(new AppSettingsStore(settingsPath));
        _scriptWatcher = new ScriptWatcher();
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _globalHotkeyService = OperatingSystem.IsWindows() ? new WindowsGlobalHotkeyService() : new NoOpGlobalHotkeyService();
        _playbackTimer.Tick += PlaybackTimerOnTick;
        DataContext = _viewModel;

        AlwaysOnTopCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => AlwaysOnTopChanged());
        ScreenShareSafeModeCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => ScreenShareSafeModeChanged());
        HorizontalMirrorCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => HorizontalMirrorChanged());
        SpeedSlider.AddHandler(RangeBase.ValueChangedEvent, (_, _) => SpeedChanged());
        TextSizeSlider.AddHandler(RangeBase.ValueChangedEvent, (_, _) => TextSizeChanged());
        WindowOpacitySlider.AddHandler(RangeBase.ValueChangedEvent, (_, _) => WindowOpacityChanged());
        ReadingMarginSlider.AddHandler(RangeBase.ValueChangedEvent, (_, _) => ReadingMarginChanged());
        CleanupTimestampsCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => CleanupOptionChanged());
        CleanupMetadataCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => CleanupOptionChanged());
        CleanupTablesCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => CleanupOptionChanged());
        HighPerformanceTextScrollingCheckBox.AddHandler(ToggleButton.IsCheckedChangedEvent, (_, _) => TextScrollingPerformanceChanged());
        PlaybackSmoothnessModeComboBox.SelectionChanged += PlaybackSmoothnessModeChanged;
        AppThemeComboBox.SelectionChanged += AppThemeComboBox_SelectionChanged;
        LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        ColorPresetComboBox.SelectionChanged += ColorPresetComboBox_SelectionChanged;
        ReadingGuideComboBox.SelectionChanged += ReadingGuideComboBox_SelectionChanged;
        TextAlignmentComboBox.SelectionChanged += TextAlignmentComboBox_SelectionChanged;

        SpeedSlider.Value = _viewModel.CalibrationWpm;
        UpdateSpeedLabel(_viewModel.CalibrationWpm);
        TextSizeSlider.Value = _viewModel.TextSize;
        UpdateTextSizeLabel(_viewModel.TextSize);
        WindowOpacitySlider.Value = _viewModel.WindowOpacity;
        UpdateWindowOpacityLabel(_viewModel.WindowOpacity);
        ReadingMarginSlider.Value = _viewModel.ReadingMargin;
        UpdateReadingMarginLabel(_viewModel.ReadingMargin);
        HighPerformanceTextScrollingCheckBox.IsChecked = _viewModel.HighPerformanceTextScrollingEnabled;
        UpdatePlaybackButtons();
        UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverIdle")));
        UpdateTogglePrompterButton();
        UpdateToggleRemoteButton();
        UpdateCleanupState();
        PlaybackSmoothnessModeComboBox.ItemsSource = new[]
        {
            Localizer.Get("LabelSmoothRenderAligned"),
            Localizer.Get("LabelSmoothOversampled")
        };
        PlaybackSmoothnessModeComboBox.SelectedIndex = 0;

        Loaded += OnMainWindowLoaded;
        Unloaded += OnMainWindowUnloaded;

        Closing += (_, _) => PrepareForShutdown();
        _globalHotkeyService.HotkeyPressed += GlobalHotkeyPressed;
    }

    public void PrepareForShutdown()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(PrepareForShutdown);
            return;
        }

        if (_isShuttingDown) return;
        _viewModel.Logger.LogInfo("PrepareForShutdown called.");
        _globalHotkeyService.Stop();
        _globalHotkeyService.Dispose();
        _scriptWatcher.Dispose();
        _isShuttingDown = true;

        try
        {
            _playbackTimer.Stop();
            _compositionPlaybackActive = false;
            _compositionFrameScheduled = false;
            _playbackSession?.TryPause();
            _playbackSession?.Reset();

            if (_prompterWindow is not null)
            {
                _lastPrompterBounds = _prompterWindow.CaptureBounds();
                _prompterWindow.PlayPauseRequested -= PrompterWindowOnPlayPauseRequested;
                _prompterWindow.ResetRequested -= PrompterWindowOnResetRequested;
                _prompterWindow.SpeedDeltaRequested -= PrompterWindowOnSpeedDeltaRequested;
                _prompterWindow.Closed -= PrompterWindowOnClosed;
                _prompterWindow.Close();
                _prompterWindow = null;
            }

            _remoteWindow?.Close();
            _remoteWindow = null;
        }
        catch
        {
            // Best-effort cleanup path during shutdown.
        }
    }

    private async void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Logger.LogError("Failed to initialize on load.", ex);
            return;
        }
        SpeedSlider.Value = _viewModel.CalibrationWpm;
        TextSizeSlider.Value = _viewModel.TextSize;
        WindowOpacitySlider.Value = _viewModel.WindowOpacity;
        ReadingMarginSlider.Value = _viewModel.ReadingMargin;
        UpdateSpeedLabel(_viewModel.CalibrationWpm);
        UpdateTextSizeLabel(_viewModel.TextSize);
        UpdateWindowOpacityLabel(_viewModel.WindowOpacity);
        UpdateReadingMarginLabel(_viewModel.ReadingMargin);
        HotkeyTextBox.Text = _viewModel.HotkeyGesture;
        NextMarkerHotkeyTextBox.Text = _viewModel.NextMarkerGesture;
        PrevMarkerHotkeyTextBox.Text = _viewModel.PrevMarkerGesture;
        PlaybackSmoothnessModeComboBox.SelectedIndex = _viewModel.PlaybackSmoothnessMode == PlaybackSmoothnessMode.OversampledTimer ? 1 : 0;
        SyncAppThemeSelection();
        SyncLanguageSelection();
        SyncColorPresetSelection();
        ReadingGuideComboBox.SelectedIndex = (int)_viewModel.ReadingGuide;
        TextAlignmentComboBox.SelectedIndex = (int)_viewModel.TextAlignment;
        HighPerformanceTextScrollingCheckBox.IsChecked = _viewModel.HighPerformanceTextScrollingEnabled;
        HorizontalMirrorCheckBox.IsChecked = _viewModel.HorizontalMirror;
        ExternalEditorPathTextBox.Text = _viewModel.ExternalEditorPath;
        ApplyPrompterScrollMode();
        RegisterOrUpdateGlobalHotkey();
        _lastRemoteBounds = await _remoteBoundsService.LoadBoundsAsync();
        await RefreshProfilesAsync();
        _viewModel.Logger.LogInfo("MainWindow loaded and preferences initialized.");
    }

    private void OnMainWindowUnloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnMainWindowLoaded;
    }

    private async void LoadScriptButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open script",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Scripts")
                {
                    Patterns = ["*.txt", "*.md", "*.srt", "*.vtt"]
                }
            ]
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null) return;

        await _viewModel.LoadScriptAsync(file.Path.LocalPath);
        if (_viewModel.LoadedScript is null) return;
        _cleanupPreviewActive = false;
        _cleanedPreviewScriptText = null;
        _activeScriptText = _viewModel.LoadedScript.Content;
        CleanupPreviewPanel.IsVisible = false;
        OriginalPreviewTextBox.Text = string.Empty;
        CleanedPreviewTextBox.Text = string.Empty;
        UpdateCleanupState();

        _scriptWatcher.StartWatching(_viewModel.LoadedScript.SourcePath, OnScriptFileChangedOnDisk);

        _prompterWindow ??= new PrompterWindow();
        _prompterWindow.PlayPauseRequested -= PrompterWindowOnPlayPauseRequested;
        _prompterWindow.ResetRequested -= PrompterWindowOnResetRequested;
        _prompterWindow.SpeedDeltaRequested -= PrompterWindowOnSpeedDeltaRequested;
        _prompterWindow.Closed -= PrompterWindowOnClosed;
        _prompterWindow.PlayPauseRequested += PrompterWindowOnPlayPauseRequested;
        _prompterWindow.ResetRequested += PrompterWindowOnResetRequested;
        _prompterWindow.SpeedDeltaRequested += PrompterWindowOnSpeedDeltaRequested;
        _prompterWindow.Closed += PrompterWindowOnClosed;
        _prompterWindow.SetScrollMode(_viewModel.PrompterScrollMode);
        _prompterWindow.NextMarkerRequested += () => NextMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
        _prompterWindow.PrevMarkerRequested += () => PreviousMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());

        ApplyScriptVariant(_viewModel.LoadedScript.Content);
        _prompterWindow.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
        ApplyAlwaysOnTopPreference();
        ApplyScreenShareSafeModePreference();

        if (_lastPrompterBounds is not null)
            _prompterWindow.RestoreBounds(_lastPrompterBounds.Value);

        _prompterWindow.Show();
        _prompterWindow.Activate();
        UpdateTogglePrompterButton();
    }

    private void EditScriptButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastEditScriptClickTimestamp, now);
        if (elapsed < TimeSpan.FromSeconds(2))
        {
            return;
        }
        _lastEditScriptClickTimestamp = now;

        if (_viewModel.TryOpenLoadedScriptInExternalEditor(out var warning))
        {
            _viewModel.SetWindowBehaviorWarning(null);
        }
        else
        {
            _viewModel.SetWindowBehaviorWarning(warning);
        }
    }

    private void OnScriptFileChangedOnDisk()
    {
        if (_isShuttingDown) return;

        Dispatcher.UIThread.Post(async () =>
        {
            if (_isShuttingDown || _viewModel.LoadedScript is null) return;

            // Capture progress and playback state before reload
            double ratio = _playbackSession?.GetProgressRatio() ?? 0.0;
            bool wasPlaying = _playbackSession?.State == PlaybackState.Playing;

            bool ok = await _viewModel.ReloadScriptAsync();
            if (_isShuttingDown || _viewModel.LoadedScript is null) return;

            if (ok)
            {
                if (CleanupPreviewPanel.IsVisible || _cleanupPreviewActive)
                {
                    RefreshCleanupPreview();
                }

                if (_cleanupPreviewActive)
                {
                    ApplyScriptVariant(_cleanedPreviewScriptText ?? _viewModel.LoadedScript.Content);
                }
                else
                {
                    ApplyScriptVariant(_viewModel.LoadedScript.Content);
                }

                if (_playbackSession is not null)
                {
                    _playbackSession.SetProgress(ratio);
                    _prompterWindow?.SetProgress(ratio, snapToTarget: true);
                    _remoteWindow?.SetProgress(ratio);

                    if (wasPlaying)
                    {
                        _playbackSession.TryPlay();
                        StartPlaybackLoop();
                        _remoteWindow?.SetPlaybackState(RemotePlaybackState.Playing);
                    }
                    else
                    {
                        _playbackSession.TryPause();
                        StopPlaybackLoop();
                        _remoteWindow?.SetPlaybackState(RemotePlaybackState.Paused);
                    }
                    UpdatePlaybackButtons();
                }
            }
        });
    }

    private void PlayButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null || _prompterWindow is null || !_playbackSession.TryPlay())
        {
            _viewModel.SetWindowBehaviorWarning(Localizer.Get("WarnLoadScriptFirst"));
            return;
        }

        _viewModel.SetWindowBehaviorWarning(null);
        StartPlaybackLoop();
        _remoteWindow?.SetPlaybackState(RemotePlaybackState.Playing);
        UpdatePlaybackButtons();
    }

    private void PauseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null) return;
        _playbackSession.TryPause();
        StopPlaybackLoop();
        _prompterWindow?.SetProgress(_playbackSession.GetProgressRatio(), snapToTarget: true);
        _remoteWindow?.SetProgress(_playbackSession.GetProgressRatio());
        _remoteWindow?.SetPlaybackState(RemotePlaybackState.Paused);
        UpdatePlaybackButtons();
    }

    private void ResetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null) return;
        _playbackSession.Reset();
        StopPlaybackLoop();
        _prompterWindow?.SetProgress(0, snapToTarget: true);
        _remoteWindow?.SetProgress(0);
        _remoteWindow?.SetPlaybackState(RemotePlaybackState.Stopped);
        UpdatePlaybackButtons();
    }

    private void NextMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null) return;
        _playbackSession.JumpToNextMarker();
        SyncPrompterPosition();
        UpdatePlaybackButtons();
    }

    private void PreviousMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null) return;
        _playbackSession.JumpToPreviousMarker();
        SyncPrompterPosition();
        UpdatePlaybackButtons();
    }

    private void SyncPrompterPosition()
    {
        if (_playbackSession is null) return;
        var ratio = _playbackSession.GetProgressRatio();
        _prompterWindow?.SetProgress(ratio, snapToTarget: true);
        _remoteWindow?.SetProgress(ratio);
        _remoteWindow?.SetPlaybackButtons(PlayButton.IsEnabled, PauseButton.IsEnabled, ResetButton.IsEnabled);
    }

    private void AddMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_playbackSession is null) return;
        var ratio = _playbackSession.GetProgressRatio();
        var label = (MarkerLabelTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(label)) label = Localizer.Get("DefaultMarkerName", _viewModel.ScriptMarkers.Count + 1);
        
        _viewModel.AddMarker(label, ratio);
        _playbackSession.SetMarkers(_viewModel.ScriptMarkers);
        MarkerLabelTextBox.Text = string.Empty;
    }

    private void RenameMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MarkersListBox.SelectedItem is ScriptMarker marker)
        {
            var label = (MarkerLabelTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(label)) return;
            
            _viewModel.UpdateMarker(marker.Id, label);
            _playbackSession?.SetMarkers(_viewModel.ScriptMarkers);
            MarkerLabelTextBox.Text = string.Empty;
        }
    }

    private void DeleteMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MarkersListBox.SelectedItem is ScriptMarker marker)
        {
            _viewModel.RemoveMarker(marker.Id);
            _playbackSession?.SetMarkers(_viewModel.ScriptMarkers);
        }
    }

    private void JumpToMarkerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MarkersListBox.SelectedItem is ScriptMarker marker && _playbackSession is not null)
        {
            _playbackSession.SetProgress(marker.ProgressRatio);
            SyncPrompterPosition();
            UpdatePlaybackButtons();
        }
    }

    private void PlaybackTimerOnTick(object? sender, EventArgs e)
    {
        AdvancePlaybackFrame(_playbackTimer.Interval);
    }

    private void AlwaysOnTopChanged()
    {
        var enabled = AlwaysOnTopCheckBox.IsChecked == true;
        _viewModel.SetAlwaysOnTop(enabled);
        ApplyAlwaysOnTopPreference();
        _ = _viewModel.SavePreferencesAsync();
    }

    private void HorizontalMirrorChanged()
    {
        var enabled = HorizontalMirrorCheckBox.IsChecked == true;
        _viewModel.SetHorizontalMirror(enabled);
        _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
        _ = _viewModel.SavePreferencesAsync();
    }

    private void ApplyAlwaysOnTopPreference()
    {
        if (_prompterWindow is null)
        {
            _viewModel.SetWindowBehaviorWarning(null);
            return;
        }

        if (_windowPriorityService.TrySetAlwaysOnTop(value => _prompterWindow.Topmost = value, _viewModel.AlwaysOnTop, out var warning))
        {
            _viewModel.SetWindowBehaviorWarning(null);
            return;
        }

        _viewModel.SetWindowBehaviorWarning(warning);
    }

    private void UpdatePlaybackButtons()
    {
        var hasPrompter = _prompterWindow is not null;
        PlayButton.IsEnabled = hasPrompter && _playbackSession?.CanPlay == true;
        PauseButton.IsEnabled = hasPrompter && _playbackSession?.CanPause == true;
        ResetButton.IsEnabled = _playbackSession?.CanReset == true;
        _remoteWindow?.SetPlaybackButtons(PlayButton.IsEnabled, PauseButton.IsEnabled, ResetButton.IsEnabled);
        if (_prompterWindow is not null)
        {
            _prompterWindow.SetPlaybackState(_playbackSession?.State == PlaybackState.Playing);
        }
    }

    private void SpeedChanged()
    {
        var wpm = (int)Math.Round(SpeedSlider.Value);
        UpdateSpeedLabel(wpm);
        _playbackSession?.SetWpm(wpm);
    }

    private void UpdateSpeedLabel(int wpm)
    {
        SpeedLabel.Text = Localizer.Get("LabelSpeed", wpm);
        _remoteWindow?.SetSpeed(wpm);
    }

    private void PlaybackSmoothnessModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var mode = PlaybackSmoothnessModeComboBox.SelectedIndex == 1
            ? PlaybackSmoothnessMode.OversampledTimer
            : PlaybackSmoothnessMode.RenderAligned;

        _viewModel.SetPlaybackSmoothnessMode(mode);
        _ = _viewModel.SavePreferencesAsync();

        if (_playbackSession?.State == PlaybackState.Playing)
        {
            StopPlaybackLoop();
            StartPlaybackLoop();
        }
    }

    private void AppThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;

        var selectedTheme = AppThemeComboBox.SelectedItem is ComboBoxItem item
            ? item.Tag as string ?? "Light"
            : "Light";
        
        if (_viewModel.AppTheme != selectedTheme)
        {
            _viewModel.SetAppTheme(selectedTheme);
            _ = _viewModel.SavePreferencesAsync();
        }

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = selectedTheme == "Dark"
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
        }
    }

    private void LanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;

        var selectedLang = LanguageComboBox.SelectedItem is ComboBoxItem item
            ? item.Tag as string ?? "Auto"
            : "Auto";

        if (_viewModel.Language != selectedLang)
        {
            _viewModel.SetLanguage(selectedLang);
            _ = _viewModel.SavePreferencesAsync();

            App.SetLanguage(selectedLang);
            RefreshPlaybackSmoothnessComboBoxItems();
            RefreshLocalizedLabels();
        }
    }

    private void RefreshPlaybackSmoothnessComboBoxItems()
    {
        var selectedIdx = PlaybackSmoothnessModeComboBox.SelectedIndex;
        PlaybackSmoothnessModeComboBox.ItemsSource = new[]
        {
            Localizer.Get("LabelSmoothRenderAligned"),
            Localizer.Get("LabelSmoothOversampled")
        };
        PlaybackSmoothnessModeComboBox.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
    }

    private void RefreshLocalizedLabels()
    {
        _viewModel.RefreshLocalizedStrings();
        UpdateSpeedLabel((int)Math.Round(SpeedSlider.Value));
        UpdateTextSizeLabel((int)Math.Round(TextSizeSlider.Value));
        UpdateWindowOpacityLabel(WindowOpacitySlider.Value);
        UpdateReadingMarginLabel((int)Math.Round(ReadingMarginSlider.Value));
        UpdateTogglePrompterButton();
        UpdateToggleRemoteButton();
        UpdateCleanupState();

        if (_playbackDriverStatus != null)
        {
            if (_playbackDriverStatus.Contains("idle") || _playbackDriverStatus.Contains("inattivo"))
                UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverIdle")));
            else if (_playbackDriverStatus.Contains("compositor"))
                UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverCompositor")));
            else if (_playbackDriverStatus.Contains("oversampled") || _playbackDriverStatus.Contains("timer sovracampionato"))
                UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverOversampled")));
            else if (_playbackDriverStatus.Contains("fallback"))
                UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverFallback")));
        }

        _remoteWindow?.RefreshLocalizedLabels();
    }

    private static readonly (string Text, string Bg)[] ColorPresets = new[]
    {
        ("#F4F8FB", "#141B22"), // Default
        ("#F8F8F2", "#282A36"), // Dracula
        ("#D8DEE9", "#2E3440"), // Nord
        ("#EBDBB2", "#282828"), // Gruvbox Dark
        ("#839496", "#002B36"), // Solarized Dark
        ("#F8F8F2", "#272822"), // Monokai
        ("#00FF00", "#000000"), // Matrix Green
        ("#F0E442", "#0072B2")  // Colorblind Safe
    };

    private void SyncColorPresetSelection()
    {
        var currentText = _viewModel.TextColor;
        var currentBg = _viewModel.BackgroundColor;
        int matchedIndex = 0; // fallback to Default
        for (int i = 0; i < ColorPresets.Length; i++)
        {
            if (string.Equals(ColorPresets[i].Text, currentText, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ColorPresets[i].Bg, currentBg, StringComparison.OrdinalIgnoreCase))
            {
                matchedIndex = i;
                break;
            }
        }
        ColorPresetComboBox.SelectedIndex = matchedIndex;
    }

    private void SyncAppThemeSelection()
    {
        var theme = _viewModel.AppTheme;
        int index = 0;
        for (int i = 0; i < AppThemeComboBox.ItemCount; i++)
        {
            if (AppThemeComboBox.ContainerFromIndex(i) is ComboBoxItem item &&
                string.Equals(item.Tag as string, theme, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }
        AppThemeComboBox.SelectedIndex = index;
    }

    private void SyncLanguageSelection()
    {
        var lang = _viewModel.Language;
        int index = 0;
        for (int i = 0; i < LanguageComboBox.ItemCount; i++)
        {
            if (LanguageComboBox.ContainerFromIndex(i) is ComboBoxItem item &&
                string.Equals(item.Tag as string, lang, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }
        LanguageComboBox.SelectedIndex = index;
    }

    private void ColorPresetComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        var idx = ColorPresetComboBox.SelectedIndex;
        if (idx >= 0 && idx < ColorPresets.Length)
        {
            var (text, bg) = ColorPresets[idx];
            if (!string.Equals(_viewModel.TextColor, text, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_viewModel.BackgroundColor, bg, StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.SetColors(text, bg);
                _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
                _ = _viewModel.SavePreferencesAsync();
            }
        }
    }

    private void ReadingGuideComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        var idx = ReadingGuideComboBox.SelectedIndex;
        if (idx >= 0 && idx <= 3)
        {
            var mode = (ReadingGuideMode)idx;
            if (_viewModel.ReadingGuide != mode)
            {
                _viewModel.SetReadingGuideMode(mode);
                _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
                _ = _viewModel.SavePreferencesAsync();
            }
        }
    }

    private void TextAlignmentComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        var idx = TextAlignmentComboBox.SelectedIndex;
        if (idx >= 0 && idx <= 3)
        {
            var alignment = (PromptinoTextAlignment)idx;
            if (_viewModel.TextAlignment != alignment)
            {
                _viewModel.SetTextAlignment(alignment);
                _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
                _ = _viewModel.SavePreferencesAsync();
            }
        }
    }

    private void TextScrollingPerformanceChanged()
    {
        var mode = HighPerformanceTextScrollingCheckBox.IsChecked == true
            ? PrompterScrollMode.HighPerformance
            : PrompterScrollMode.Basic;

        _viewModel.SetPrompterScrollMode(mode);
        ApplyPrompterScrollMode();
        _ = _viewModel.SavePreferencesAsync();
    }

    private void TextSizeChanged()
    {
        var size = (int)Math.Round(TextSizeSlider.Value);
        _viewModel.SetTextSize(size);
        UpdateTextSizeLabel(size);
        _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
        SyncPrompterProgressAfterStyleChange();
        _ = _viewModel.SavePreferencesAsync();
    }

    private void WindowOpacityChanged()
    {
        var opacity = Math.Clamp(WindowOpacitySlider.Value, ReadingPreferences.MinOpacity, ReadingPreferences.MaxOpacity);
        _viewModel.SetWindowOpacity(opacity);
        UpdateWindowOpacityLabel(opacity);
        _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
        SyncPrompterProgressAfterStyleChange();
        _ = _viewModel.SavePreferencesAsync();
    }

    private void ReadingMarginChanged()
    {
        var margin = (int)Math.Round(ReadingMarginSlider.Value);
        _viewModel.SetReadingMargin(margin);
        UpdateReadingMarginLabel(margin);
        _prompterWindow?.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
        SyncPrompterProgressAfterStyleChange();
        _ = _viewModel.SavePreferencesAsync();
    }

    private void UpdateTextSizeLabel(int size) => TextSizeLabel.Text = Localizer.Get("LabelTextSize", size);

    private void UpdateWindowOpacityLabel(double opacity) => WindowOpacityLabel.Text = Localizer.Get("LabelWindowOpacity", (int)Math.Round(opacity * 100));

    private void UpdateReadingMarginLabel(int margin) => ReadingMarginLabel.Text = Localizer.Get("LabelReadingMargin", margin);

    private void PrompterWindowOnPlayPauseRequested()
    {
        if (_playbackSession?.State == PlaybackState.Playing)
        {
            PauseButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            return;
        }

        PlayButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    }

    private void PrompterWindowOnResetRequested() => ResetButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());

    private void PrompterWindowOnSpeedDeltaRequested(int delta)
    {
        var next = Math.Clamp((int)Math.Round(SpeedSlider.Value) + delta, ReadingSpeed.MinWpm, ReadingSpeed.MaxWpm);
        SpeedSlider.Value = next;
    }

    private void PrompterWindowOnClosed(object? sender, EventArgs e)
    {
        StopPlaybackLoop();
        _playbackSession?.Reset();
        _lastPrompterBounds = _prompterWindow?.CaptureBounds();
        _prompterWindow = null;
        UpdatePlaybackButtons();
        UpdateTogglePrompterButton();
    }

    private void ScreenShareSafeModeChanged()
    {
        _viewModel.SetScreenShareSafeModeEnabled(ScreenShareSafeModeCheckBox.IsChecked == true);
        ApplyScreenShareSafeModePreference();
    }

    private void ApplyScreenShareSafeModePreference()
    {
        var warned = false;

        if (_prompterWindow is not null)
        {
            var handle = _prompterWindow.TryGetPlatformHandle()?.Handle ?? 0;
            if (!_windowPriorityService.TrySetScreenShareSafeMode(handle, _viewModel.ScreenShareSafeModeEnabled, out var warning))
            {
                _viewModel.SetWindowBehaviorWarning(warning);
                warned = true;
            }
        }

        if (_remoteWindow is not null)
        {
            var handle = _remoteWindow.TryGetPlatformHandle()?.Handle ?? 0;
            if (!_windowPriorityService.TrySetScreenShareSafeMode(handle, _viewModel.ScreenShareSafeModeEnabled, out var warning))
            {
                _viewModel.SetWindowBehaviorWarning(warning);
                warned = true;
            }
        }

        if (!warned) _viewModel.SetWindowBehaviorWarning(null);
    }

    private void TogglePrompterButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_prompterWindow is null)
        {
            _prompterWindow = new PrompterWindow();
            _prompterWindow.Closed += PrompterWindowOnClosed;
            _prompterWindow.PlayPauseRequested += PrompterWindowOnPlayPauseRequested;
            _prompterWindow.ResetRequested += PrompterWindowOnResetRequested;
            _prompterWindow.SpeedDeltaRequested += PrompterWindowOnSpeedDeltaRequested;
            _prompterWindow.NextMarkerRequested += () => NextMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            _prompterWindow.PrevMarkerRequested += () => PreviousMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            _prompterWindow.SetScrollMode(_viewModel.PrompterScrollMode);
            if (_lastPrompterBounds is not null) _prompterWindow.RestoreBounds(_lastPrompterBounds.Value);
            
            if (_playbackSession is null && _viewModel.LoadedScript is not null)
            {
                var scriptText = string.IsNullOrEmpty(_activeScriptText) ? _viewModel.LoadedScript.Content : _activeScriptText;
                ApplyScriptVariant(scriptText);
            }
            else
            {
                ApplyScriptToPrompter(_displayScriptText);
                if (_playbackSession is not null)
                    _prompterWindow.SetProgress(_playbackSession.GetProgressRatio(), snapToTarget: true);
            }
            _prompterWindow.ApplyReadingStyle(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.ReadingMargin, _viewModel.WindowOpacity, _viewModel.FontFamily, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment);
            _prompterWindow.Show();
            _prompterWindow.Activate();
            ApplyAlwaysOnTopPreference();
            ApplyScreenShareSafeModePreference();
            UpdatePlaybackButtons();
        }
        else
        {
            _lastPrompterBounds = _prompterWindow.CaptureBounds();
            PauseButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            _prompterWindow.Close();
        }

        UpdateTogglePrompterButton();
    }

    private void ToggleRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_remoteWindow is null)
        {
            _remoteWindow = new RemoteMiniWindow();
            _remoteWindow.PlayRequested += RemoteWindowOnPlayRequested;
            _remoteWindow.PauseRequested += RemoteWindowOnPauseRequested;
            _remoteWindow.ResetRequested += RemoteWindowOnResetRequested;
            _remoteWindow.SpeedDeltaRequested += RemoteWindowOnSpeedDeltaRequested;
            _remoteWindow.NextMarkerRequested += RemoteWindowOnNextMarkerRequested;
            _remoteWindow.PrevMarkerRequested += RemoteWindowOnPrevMarkerRequested;
            _remoteWindow.PositionChanged += RemoteWindowOnPositionChanged;
            _remoteWindow.SizeChanged += RemoteWindowOnSizeChanged;
            _remoteWindow.Closed += RemoteWindowOnClosed;

            if (_lastRemoteBounds is not null)
            {
                _remoteWindow.Position = new PixelPoint(_lastRemoteBounds.Value.X, _lastRemoteBounds.Value.Y);
                _remoteWindow.Width = _lastRemoteBounds.Value.Width;
                _remoteWindow.Height = _lastRemoteBounds.Value.Height;
            }

            _remoteWindow.SetSpeed((int)Math.Round(SpeedSlider.Value));
            _remoteWindow.SetPlaybackButtons(PlayButton.IsEnabled, PauseButton.IsEnabled, ResetButton.IsEnabled);
            _remoteWindow.Show();
            ApplyScreenShareSafeModePreference();
        }
        else
        {
            SaveRemoteBoundsBestEffort();
            _remoteWindow.Close();
            // Subscriptions will be cleared in RemoteWindowOnClosed when Close() completes.
        }

        UpdateToggleRemoteButton();
    }

    private void RemoteWindowOnPlayRequested() => PlayButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    private void RemoteWindowOnPauseRequested() => PauseButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    private void RemoteWindowOnResetRequested() => ResetButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    private void RemoteWindowOnNextMarkerRequested() => NextMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    private void RemoteWindowOnPrevMarkerRequested() => PreviousMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
    
    private void RemoteWindowOnSpeedDeltaRequested(int delta)
    {
        var next = Math.Clamp((int)Math.Round(SpeedSlider.Value) + delta, ReadingSpeed.MinWpm, ReadingSpeed.MaxWpm);
        SpeedSlider.Value = next;
    }

    private void RemoteWindowOnPositionChanged(object? sender, PixelPointEventArgs e) => SaveRemoteBoundsBestEffort();
    private void RemoteWindowOnSizeChanged(object? sender, SizeChangedEventArgs e) => SaveRemoteBoundsBestEffort();

    private void RemoteWindowOnClosed(object? sender, EventArgs e)
    {
        if (_remoteWindow is not null)
        {
            _remoteWindow.PlayRequested -= RemoteWindowOnPlayRequested;
            _remoteWindow.PauseRequested -= RemoteWindowOnPauseRequested;
            _remoteWindow.ResetRequested -= RemoteWindowOnResetRequested;
            _remoteWindow.SpeedDeltaRequested -= RemoteWindowOnSpeedDeltaRequested;
            _remoteWindow.NextMarkerRequested -= RemoteWindowOnNextMarkerRequested;
            _remoteWindow.PrevMarkerRequested -= RemoteWindowOnPrevMarkerRequested;
            _remoteWindow.PositionChanged -= RemoteWindowOnPositionChanged;
            _remoteWindow.SizeChanged -= RemoteWindowOnSizeChanged;
            _remoteWindow.Closed -= RemoteWindowOnClosed;
            
            SaveRemoteBoundsBestEffort();
            _remoteWindow = null;
        }
        UpdateToggleRemoteButton();
    }

    private void CleanupOptionChanged()
    {
        if (CleanupPreviewPanel.IsVisible)
            RefreshCleanupPreview();
    }

    private void PreviewCleanupButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.LoadedScript is null) return;
        RefreshCleanupPreview();
        _cleanupPreviewActive = false;
        UpdateCleanupState();
    }

    private void ApplyCleanupButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.LoadedScript is null) return;
        RefreshCleanupPreview();
        ApplyScriptVariant(_cleanedPreviewScriptText ?? _viewModel.LoadedScript.Content);
        _cleanupPreviewActive = true;
        UpdateCleanupState();
    }

    private void RestoreOriginalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.LoadedScript is null) return;
        ApplyScriptVariant(_viewModel.LoadedScript.Content);
        RefreshCleanupPreview();
        _cleanupPreviewActive = false;
        UpdateCleanupState();
    }

    private void ApplyScriptToPrompter(string scriptText) => _prompterWindow?.SetScriptText(scriptText);
    private void UpdateTogglePrompterButton() => TogglePrompterButton.Content = _prompterWindow is null ? Localizer.Get("BtnShowPrompter") : Localizer.Get("BtnHidePrompter");
    private void UpdateCleanupState() => CleanupStateLabel.Text = _cleanupPreviewActive
        ? Localizer.Get("CleanupStateCleaned")
        : CleanupPreviewPanel.IsVisible
            ? Localizer.Get("CleanupStatePreviewReady")
            : Localizer.Get("CleanupStateLoaded");

    private void ApplyScriptVariant(string scriptText)
    {
        _activeScriptText = scriptText;
        _displayScriptText = ScriptMarkerParser.ParseAndRemoveMarkers(scriptText, out var markers);
        ApplyScriptToPrompter(_displayScriptText);

        if (_viewModel.LoadedScript is null) return;

        _playbackSession = new PlaybackSession(_displayScriptText, _viewModel.CalibrationWpm);
        _playbackSession.SetWpm((int)SpeedSlider.Value);
        _playbackSession.SetMarkers(markers);
        _viewModel.SetMarkers(markers);
        _playbackTimer.Stop();
        _prompterWindow?.SetProgress(0, snapToTarget: true);
        UpdatePlaybackButtons();
    }

    private void RefreshCleanupPreview()
    {
        if (_viewModel.LoadedScript is null) return;

        var original = _viewModel.LoadedScript.RawContent ?? _viewModel.LoadedScript.Content;
        var cleaned = BuildCleanedPreviewText();
        _cleanedPreviewScriptText = cleaned;
        OriginalPreviewTextBox.Text = TruncateForPreview(original);
        CleanedPreviewTextBox.Text = TruncateForPreview(cleaned);
        CleanupPreviewPanel.IsVisible = true;
    }

    private static string TruncateForPreview(string text, int maxLines = 50)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var lines = text.Split('\n');
        if (lines.Length <= maxLines) return text;
        return string.Join('\n', System.Linq.Enumerable.Take(lines, maxLines)) + "\n\n... [Preview truncated for performance]";
    }

    private string BuildCleanedPreviewText()
    {
        if (_viewModel.LoadedScript is null) return string.Empty;

        var raw = _viewModel.LoadedScript.RawContent ?? _viewModel.LoadedScript.Content;
        return new ScriptTextTransformer().Transform(
            raw,
            System.IO.Path.GetExtension(_viewModel.LoadedScript.SourcePath),
            CreateCleanupOptions(
                CleanupTimestampsCheckBox.IsChecked == true,
                CleanupMetadataCheckBox.IsChecked == true,
                CleanupTablesCheckBox.IsChecked == true));
    }

    public static ScriptTextTransformer.ScriptCleanupOptions CreateCleanupOptions(
        bool removeTimestamps,
        bool removeMetadataRows,
        bool removeMarkdownTables)
        => new(
            RemoveTimestamps: removeTimestamps,
            RemoveMetadataRows: removeMetadataRows,
            RemoveMarkdownTables: removeMarkdownTables);

    private void SyncPrompterProgressAfterStyleChange()
    {
        if (_playbackSession is null || _prompterWindow is null) return;
        var ratio = _playbackSession.GetProgressRatio();
        Dispatcher.UIThread.Post(() =>
        {
            if (_prompterWindow is not null && !_isShuttingDown)
                _prompterWindow.SetProgress(ratio, snapToTarget: true);
        }, DispatcherPriority.Render);
    }

    private void ApplyPrompterScrollMode()
    {
        if (_prompterWindow is null) return;

        _prompterWindow.SetScrollMode(_viewModel.PrompterScrollMode);
        if (_playbackSession is not null)
            _prompterWindow.SetProgress(_playbackSession.GetProgressRatio(), snapToTarget: true);
    }

    private void UpdateToggleRemoteButton() => ToggleRemoteButton.Content = _remoteWindow is null ? Localizer.Get("BtnShowRemote") : Localizer.Get("BtnHideRemote");

    private void StartPlaybackLoop()
    {
        _lastTickTimestamp = Stopwatch.GetTimestamp();

        if (_viewModel.PlaybackSmoothnessMode == PlaybackSmoothnessMode.OversampledTimer)
        {
            _compositionPlaybackActive = false;
            _compositionFrameScheduled = false;
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(8);
            _playbackTimer.Start();
            UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverOversampled")));
            return;
        }

        _playbackTimer.Stop();
        _compositionPlaybackActive = true;
        _compositionFrameScheduled = false;
        ScheduleCompositionFrame();
    }

    private void StopPlaybackLoop()
    {
        _playbackTimer.Stop();
        _compositionPlaybackActive = false;
        _compositionFrameScheduled = false;
        UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverIdle")));
    }

    private void ScheduleCompositionFrame()
    {
        if (!_compositionPlaybackActive || _compositionFrameScheduled) return;
        if (!TryRequestCompositionFrame())
        {
            _compositionPlaybackActive = false;
            _compositionFrameScheduled = false;
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(16);
            _playbackTimer.Start();
            UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverFallback")));
            return;
        }
    }

    private void OnCompositionFrame()
    {
        _compositionFrameScheduled = false;
        if (!_compositionPlaybackActive) return;

        ScheduleCompositionFrame();
        AdvancePlaybackFrame(TimeSpan.FromMilliseconds(16));
    }

    private void AdvancePlaybackFrame(TimeSpan fallbackElapsed)
    {
        if (_playbackSession is null || _prompterWindow is null)
        {
            StopPlaybackLoop();
            return;
        }

        var previousState = _playbackSession.State;
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTickTimestamp, now);
        _lastTickTimestamp = now;
        _playbackSession.Advance(elapsed <= TimeSpan.Zero ? fallbackElapsed : elapsed);
        var ratio = _playbackSession.GetProgressRatio();
        _prompterWindow.SetProgress(ratio);
        _remoteWindow?.SetProgress(ratio);
        if (_playbackSession.State != PlaybackState.Playing)
        {
            StopPlaybackLoop();
            _prompterWindow.SetProgress(ratio, snapToTarget: true);
            _remoteWindow?.SetPlaybackState(RemotePlaybackState.Stopped);
        }

        if (_playbackSession.State != previousState)
            UpdatePlaybackButtons();
    }

    private bool TryRequestCompositionFrame()
    {
        if (_prompterWindow is null)
            return false;

        try
        {
            var compositor = ElementComposition.GetElementVisual(_prompterWindow)?.Compositor;
            if (compositor is null)
                return false;

            _compositionFrameScheduled = true;
            compositor.RequestCompositionUpdate(OnCompositionFrame);
            UpdatePlaybackDriverStatus(Localizer.Get("DriverStatus", Localizer.Get("DriverCompositor")));
            return true;
        }
        catch (InvalidOperationException)
        {
            _compositionFrameScheduled = false;
            return false;
        }
    }

    private void UpdatePlaybackDriverStatus(string status)
    {
        if (_playbackDriverStatus == status) return;
        _playbackDriverStatus = status;
        PlaybackDriverStatusTextBlock.Text = status;
    }

    private const int HotkeyIdPlayPause = 0x504D;
    private const int HotkeyIdNextMarker = 0x504E;
    private const int HotkeyIdPrevMarker = 0x5050;

    private void RecordPlayPauseHotkeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_recordingTarget == HotkeyTextBox)
            StopRecording();
        else
            StartRecording(HotkeyTextBox, RecordPlayPauseHotkeyButton);
    }

    private void RecordNextMarkerHotkeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_recordingTarget == NextMarkerHotkeyTextBox)
            StopRecording();
        else
            StartRecording(NextMarkerHotkeyTextBox, RecordNextMarkerHotkeyButton);
    }

    private void RecordPrevMarkerHotkeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_recordingTarget == PrevMarkerHotkeyTextBox)
            StopRecording();
        else
            StartRecording(PrevMarkerHotkeyTextBox, RecordPrevMarkerHotkeyButton);
    }

    private void StartRecording(TextBox targetTextBox, Button recordingButton)
    {
        if (_recordingTarget != null)
        {
            StopRecording();
        }

        _recordingTarget = targetTextBox;
        _recordingButton = recordingButton;
        _recordingButton.Content = Localizer.Get("BtnRecording");
        _viewModel.SetWindowBehaviorWarning(Localizer.Get("WarnPressKeys"));
    }

    private void StopRecording()
    {
        if (_recordingButton != null)
        {
            _recordingButton.Content = Localizer.Get("BtnRecord");
        }
        _recordingTarget = null;
        _recordingButton = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_recordingTarget != null)
        {
            var key = e.Key;

            // Handle Escape to cancel
            if (key == Key.Escape)
            {
                _viewModel.SetWindowBehaviorWarning(null);
                StopRecording();
                e.Handled = true;
                return;
            }

            // Handle Backspace/Delete to clear
            if (key == Key.Back || key == Key.Delete)
            {
                _recordingTarget.Text = string.Empty;
                _viewModel.SetWindowBehaviorWarning(null);
                StopRecording();
                e.Handled = true;
                return;
            }

            // Skip modifier-only key presses
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            var keyStr = GetKeyString(key);
            if (keyStr == null)
            {
                _viewModel.SetWindowBehaviorWarning(Localizer.Get("WarnUnsupportedKey"));
                e.Handled = true;
                return;
            }

            var modifiers = e.KeyModifiers;
            if (modifiers == KeyModifiers.None)
            {
                _viewModel.SetWindowBehaviorWarning(Localizer.Get("WarnModifierRequired"));
                e.Handled = true;
                return;
            }

            // Build gesture string
            var parts = new List<string>();
            if ((modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifiers & KeyModifiers.Meta) != 0) parts.Add("Win");

            parts.Add(keyStr);

            string gesture = string.Join("+", parts);
            _recordingTarget.Text = gesture;

            _viewModel.SetWindowBehaviorWarning(null); // Clear any previous warning
            StopRecording();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static string? GetKeyString(Key key)
    {
        if (key == Key.Space) return "Space";
        if (key == Key.PageDown) return "PageDown";
        if (key == Key.PageUp) return "PageUp";
        if (key >= Key.A && key <= Key.Z) return ((char)('A' + (key - Key.A))).ToString();
        if (key >= Key.D0 && key <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return ((char)('0' + (key - Key.NumPad0))).ToString();
        return null;
    }

    private void ApplyHotkeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool allOk = true;
        if (!_viewModel.TrySetHotkeyGesture(HotkeyTextBox.Text ?? string.Empty, out var w1))
        {
            _viewModel.SetWindowBehaviorWarning(w1);
            allOk = false;
        }
        if (!_viewModel.TrySetNextMarkerGesture(NextMarkerHotkeyTextBox.Text ?? string.Empty, out var w2))
        {
            _viewModel.SetWindowBehaviorWarning(w2);
            allOk = false;
        }
        if (!_viewModel.TrySetPrevMarkerGesture(PrevMarkerHotkeyTextBox.Text ?? string.Empty, out var w3))
        {
            _viewModel.SetWindowBehaviorWarning(w3);
            allOk = false;
        }

        if (allOk)
        {
            RegisterOrUpdateGlobalHotkey();
            _ = _viewModel.SavePreferencesAsync();
        }
    }

    private void RegisterOrUpdateGlobalHotkey()
    {
        var hotkeys = new List<(int Id, GlobalHotkey Hotkey)>();

        if (_viewModel.TryGetParsedHotkey(out var playPauseHotkey))
            hotkeys.Add((HotkeyIdPlayPause, playPauseHotkey));

        if (_viewModel.TryGetParsedNextMarkerHotkey(out var nextHotkey))
            hotkeys.Add((HotkeyIdNextMarker, nextHotkey));

        if (_viewModel.TryGetParsedPrevMarkerHotkey(out var prevHotkey))
            hotkeys.Add((HotkeyIdPrevMarker, prevHotkey));

        if (hotkeys.Count == 0)
        {
            _viewModel.Logger.LogWarning("No valid hotkeys configured during registration.");
            _viewModel.SetWindowBehaviorWarning("No valid hotkeys configured.");
            return;
        }

        var result = _globalHotkeyService.UpdateHotkeys(hotkeys);
        if (result.Success)
        {
            _viewModel.Logger.LogInfo("Global hotkeys registered/updated successfully.");
        }
        else
        {
            _viewModel.Logger.LogWarning($"Global hotkey registration failed: {result.Warning}");
        }
        _viewModel.SetWindowBehaviorWarning(result.Success ? null : result.Warning);
    }

    private void GlobalHotkeyPressed(int id)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_playbackSession is null)
            {
                _viewModel.SetWindowBehaviorWarning("Global hotkey received but no script is loaded.");
                return;
            }

            switch (id)
            {
                case HotkeyIdPlayPause:
                    PrompterWindowOnPlayPauseRequested();
                    break;
                case HotkeyIdNextMarker:
                    NextMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                    break;
                case HotkeyIdPrevMarker:
                    PreviousMarkerButton_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                    break;
            }
        });
    }

    private async void BrowseExternalEditorButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select External Editor",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable/Command Files")
                {
                    Patterns = ["*.exe", "*.cmd", "*.bat", "*"]
                }
            ]
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null) return;

        ExternalEditorPathTextBox.Text = file.Path.LocalPath;
    }

    private void ApplyExternalEditorPathButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetExternalEditorPath(ExternalEditorPathTextBox.Text);
        _ = _viewModel.SavePreferencesAsync();
    }

    private async Task RefreshProfilesAsync()
    {
        var load = await _profileCoordinator.LoadProfilesAsync();
        _viewModel.SetProfilesWarning(load.Recovered ? Localizer.Get("WarnProfilesReadRecovery") : null);
        ProfilesComboBox.ItemsSource = _profileCoordinator.Profiles.Select(p => p.Name).ToList();
    }

    private async void SaveProfileButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = (ProfileNameTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _viewModel.SetWindowBehaviorWarning(Localizer.Get("WarnProfileNameRequired"));
            return;
        }

        var bounds = _prompterWindow?.CaptureBounds() ?? _lastPrompterBounds ?? new PixelRect(100, 100, 900, 560);
        var profile = new SavedProfile(
            name,
            (int)Math.Round(SpeedSlider.Value),
            new ReadingPreferences(_viewModel.TextSize, _viewModel.LineSpacing, _viewModel.WindowOpacity, _viewModel.AlwaysOnTop, _viewModel.FontFamily, _viewModel.ReadingMargin, _viewModel.HorizontalMirror, _viewModel.TextColor, _viewModel.BackgroundColor, _viewModel.ReadingGuide, _viewModel.TextAlignment),
            _viewModel.AlwaysOnTop,
            _viewModel.ScreenShareSafeModeEnabled,
            _viewModel.HotkeyGesture,
            bounds.X, bounds.Y, bounds.Width, bounds.Height);

        var ok = await _profileCoordinator.SaveProfileAsync(profile);
        if (!ok)
        {
            _viewModel.SetProfilesWarning(Localizer.Get("WarnProfilesSaveFailure"));
            return;
        }

        await RefreshProfilesAsync();
        ProfilesComboBox.SelectedItem = name;
        _viewModel.SetProfilesWarning(null);
    }

    private async void LoadProfileButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ProfilesComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) return;
        var profile = _profileCoordinator.GetProfile(selected);
        if (profile is null) return;
        _viewModel.SetProfilesWarning(null);

        _viewModel.SetPersonalization(profile.Preferences.TextSize, profile.Preferences.LineSpacing, profile.Preferences.WindowOpacity, profile.Preferences.AlwaysOnTop, profile.Preferences.FontFamily, profile.Preferences.ReadingMargin, profile.Preferences.HorizontalMirror, profile.Preferences.TextColor, profile.Preferences.BackgroundColor, profile.Preferences.ReadingGuide, profile.Preferences.TextAlignment);
        SyncColorPresetSelection();
        ReadingGuideComboBox.SelectedIndex = (int)_viewModel.ReadingGuide;
        TextAlignmentComboBox.SelectedIndex = (int)_viewModel.TextAlignment;
        HorizontalMirrorCheckBox.IsChecked = _viewModel.HorizontalMirror;
        _viewModel.SetAlwaysOnTop(profile.AlwaysOnTop);
        _viewModel.SetScreenShareSafeModeEnabled(profile.ScreenShareSafeMode);
        _viewModel.TrySetHotkeyGesture(profile.HotkeyGesture, out _);
        SpeedSlider.Value = Math.Clamp(profile.Wpm, ReadingSpeed.MinWpm, ReadingSpeed.MaxWpm);
        TextSizeSlider.Value = _viewModel.TextSize;
        WindowOpacitySlider.Value = _viewModel.WindowOpacity;
        ReadingMarginSlider.Value = _viewModel.ReadingMargin;
        HotkeyTextBox.Text = _viewModel.HotkeyGesture;
        NextMarkerHotkeyTextBox.Text = _viewModel.NextMarkerGesture;
        PrevMarkerHotkeyTextBox.Text = _viewModel.PrevMarkerGesture;
        RegisterOrUpdateGlobalHotkey();
        _lastPrompterBounds = new PixelRect(profile.WindowX, profile.WindowY, profile.WindowWidth, profile.WindowHeight);
        if (_prompterWindow is not null) _prompterWindow.RestoreBounds(_lastPrompterBounds.Value);
        ApplyAlwaysOnTopPreference();
        ApplyScreenShareSafeModePreference();
        _ = _viewModel.SavePreferencesAsync();
        await Task.CompletedTask;
    }

    private async void DeleteProfileButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ProfilesComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) return;
        var ok = await _profileCoordinator.DeleteProfileAsync(selected);
        if (!ok)
        {
            _viewModel.SetProfilesWarning(Localizer.Get("WarnProfilesDeleteFailure"));
            return;
        }

        await RefreshProfilesAsync();
    }

    private async void SaveRemoteBoundsBestEffort()
    {
        if (_remoteWindow is null) return;
        var b = new PixelRect((int)_remoteWindow.Position.X, (int)_remoteWindow.Position.Y, (int)_remoteWindow.Width, (int)_remoteWindow.Height);
        _lastRemoteBounds = b;
        try
        {
            await _remoteBoundsService.SaveBoundsAsync(b);
        }
        catch
        {
            // Best-effort save during shutdown, ignore failures.
        }
    }
}
