using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Promptino.App;

public enum RemotePlaybackState
{
    Stopped,
    Playing,
    Paused
}

public partial class RemoteMiniWindow : Window
{
    public event Action? PlayRequested;
    public event Action? PauseRequested;
    public event Action? ResetRequested;
    public event Action<int>? SpeedDeltaRequested;
    public event Action? NextMarkerRequested;
    public event Action? PrevMarkerRequested;

    private RemotePlaybackState _state = RemotePlaybackState.Stopped;
    private readonly ScaleTransform _progressScale = new();

    public RemoteMiniWindow()
    {
        InitializeComponent();
        RemoteProgressFill.RenderTransform = _progressScale;
        RemoteProgressFill.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        UpdateStateDisplay();
    }

    public void SetSpeed(int wpm)
    {
        RemoteSpeedLabel.Text = $"{wpm} WPM";
    }

    public void SetProgress(double ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        _progressScale.ScaleX = ratio;
        var percent = (int)Math.Round(ratio * 100.0);
        RemoteProgressLabel.Text = $"{percent}%";
    }

    public void SetPlaybackState(RemotePlaybackState state)
    {
        _state = state;
        UpdateStateDisplay();
    }

    public void SetPlaybackButtons(bool canPlay, bool canPause, bool canReset)
    {
        RemotePlayButton.IsEnabled = canPlay;
        RemotePauseButton.IsEnabled = canPause;
        RemoteResetButton.IsEnabled = canReset;
    }

    private void UpdateStateDisplay()
    {
        IBrush? Res(string key) => Application.Current?.FindResource(key) as IBrush;

        switch (_state)
        {
            case RemotePlaybackState.Playing:
                RemoteStatusIcon.Text = "\u25B6";
                RemoteStatusIcon.Foreground = Res("PromptinoBrandAccent") ?? Brushes.Teal;
                RemoteStatusLabel.Text = Services.Localizer.Get("RemoteStatusPlaying");
                RemoteStatusLabel.Foreground = Res("PromptinoStatusPlayingForeground") ?? Brushes.White;
                break;
            case RemotePlaybackState.Paused:
                RemoteStatusIcon.Text = "\u23F8";
                RemoteStatusIcon.Foreground = Res("PromptinoStatusPausedIcon") ?? Brushes.Orange;
                RemoteStatusLabel.Text = Services.Localizer.Get("RemoteStatusPaused");
                RemoteStatusLabel.Foreground = Res("PromptinoStatusPausedForeground") ?? Brushes.Yellow;
                break;
            case RemotePlaybackState.Stopped:
            default:
                RemoteStatusIcon.Text = "\u23F9";
                RemoteStatusIcon.Foreground = Res("PromptinoStatusStoppedForeground") ?? Brushes.Gray;
                RemoteStatusLabel.Text = Services.Localizer.Get("RemoteStatusReady");
                RemoteStatusLabel.Foreground = Res("PromptinoStatusStoppedForeground") ?? Brushes.Gray;
                break;
        }
    }

    public void RefreshLocalizedLabels() => UpdateStateDisplay();

    private void Play_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => PlayRequested?.Invoke();
    private void Pause_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => PauseRequested?.Invoke();
    private void Reset_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ResetRequested?.Invoke();
    private void Up_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SpeedDeltaRequested?.Invoke(5);
    private void Down_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SpeedDeltaRequested?.Invoke(-5);
    private void NextMarker_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NextMarkerRequested?.Invoke();
    private void PrevMarker_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => PrevMarkerRequested?.Invoke();
}
