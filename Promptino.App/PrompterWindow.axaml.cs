using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Media;
using Promptino.Storage.Settings;
using System;

namespace Promptino.App;

public partial class PrompterWindow : Window
{
    private const Key PlaybackToggleKey = Key.Space;

    private readonly PrompterScrollSmoother _scrollSmoother = new();
    private readonly TranslateTransform _scriptTranslateTransform = new();
    private readonly ScaleTransform _scriptScaleTransform = new();
    private PrompterScrollMode _scrollMode = PrompterScrollMode.HighPerformance;
    private bool _isResizing;
    private bool _snapProgressOnNextLayout;
    private Point _resizeStart;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    public event Action? PlayPauseRequested;
    public event Action? ResetRequested;
    public event Action<int>? SpeedDeltaRequested;
    public event Action? NextMarkerRequested;
    public event Action? PrevMarkerRequested;

    public PrompterWindow()
    {
        InitializeComponent();

        var group = new TransformGroup();
        group.Children.Add(_scriptTranslateTransform);
        group.Children.Add(_scriptScaleTransform);
        ScriptTextBlock.RenderTransform = group;
        ScriptTextBlock.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        ScriptScrollViewer.LayoutUpdated += (_, _) => RefreshScrollableHeight(snapToTarget: _snapProgressOnNextLayout, clearPendingSnap: true);
        ScriptScrollViewer.SizeChanged += ScriptScrollViewer_SizeChanged;
        ResizeGrip.PointerPressed += ResizeGrip_PointerPressed;
        ResizeGrip.PointerMoved += ResizeGrip_PointerMoved;
        ResizeGrip.PointerReleased += ResizeGrip_PointerReleased;
        ResizeGrip.PointerEntered += (_, _) => ResizeGrip.Opacity = 1.0;
        ResizeGrip.PointerExited += (_, _) => ResizeGrip.Opacity = IsActive ? 0.9 : 0.6;

        Activated += (_, _) => UpdateFocusHint();
        Deactivated += (_, _) => UpdateFocusHint();
        UpdateFocusHint();
    }

    private void ScriptScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var height = e.NewSize.Height;
        if (height <= 0) return;
        var topSpace = height * 0.35;
        var bottomSpace = height * 0.65;
        ScriptTextBlock.Padding = new Thickness(0, topSpace, 0, bottomSpace);
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ResizeGrip).Properties.IsLeftButtonPressed) return;
        _isResizing = true;
        _resizeStart = e.GetPosition(this);
        _resizeStartWidth = Width;
        _resizeStartHeight = Height;
        e.Pointer.Capture(ResizeGrip);
        e.Handled = true;
    }

    private void ResizeGrip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing) return;
        var current = e.GetPosition(this);
        var dx = current.X - _resizeStart.X;
        var dy = current.Y - _resizeStart.Y;
        Width = Math.Max(MinWidth, _resizeStartWidth + dx);
        Height = Math.Max(MinHeight, _resizeStartHeight + dy);
        e.Handled = true;
    }

    private void ResizeGrip_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizing) return;
        _isResizing = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    public void SetScriptText(string text)
    {
        ScriptTextBlock.Text = text;
        ScriptScrollViewer.Offset = default;
        _scrollSmoother.UpdateProgress(0, snapToTarget: true);
        _snapProgressOnNextLayout = true;
        ApplyVisualScrollOffset();
    }

    public void SetScrollMode(PrompterScrollMode mode)
    {
        if (_scrollMode == mode) return;

        _scrollMode = mode;
        _snapProgressOnNextLayout = true;

        if (_scrollMode == PrompterScrollMode.Basic)
            _scriptTranslateTransform.Y = 0;
        else
            ScriptScrollViewer.Offset = default;
    }

    public void SetProgress(double ratio, bool snapToTarget = false)
    {
        ratio = Math.Clamp(ratio, 0, 1);

        if (_scrollMode == PrompterScrollMode.Basic)
        {
            _scrollSmoother.UpdateProgress(ratio, snapToTarget: true);
            ApplyBasicScrollOffset(ratio);
            return;
        }

        if (snapToTarget)
            _snapProgressOnNextLayout = true;

        RefreshScrollableHeight(snapToTarget, clearPendingSnap: false);
        _scrollSmoother.UpdateProgress(ratio, snapToTarget);
        ApplyVisualScrollOffset();
    }

    public PixelRect CaptureBounds() => new((int)Position.X, (int)Position.Y, (int)Width, (int)Height);
    public void RestoreBounds(PixelRect bounds)
    {
        Position = new PixelPoint(bounds.X, bounds.Y);
        Width = Math.Max(MinWidth, bounds.Width);
        Height = Math.Max(MinHeight, bounds.Height);
    }

    public void ApplyReadingStyle(int textSize, double lineSpacing, int readingMargin, double opacity, string fontFamily, bool horizontalMirror, string? textColor = null, string? backgroundColor = null, ReadingGuideMode readingGuide = ReadingGuideMode.Both, PromptinoTextAlignment textAlignment = PromptinoTextAlignment.Left)
    {
        ScriptTextBlock.FontSize = textSize;
        ScriptTextBlock.LineHeight = textSize * lineSpacing;
        ScriptTextBlock.FontFamily = new Avalonia.Media.FontFamily(fontFamily);
        ScriptTextBlock.TextAlignment = textAlignment switch
        {
            PromptinoTextAlignment.Center => Avalonia.Media.TextAlignment.Center,
            PromptinoTextAlignment.Right => Avalonia.Media.TextAlignment.Right,
            PromptinoTextAlignment.Justify => Avalonia.Media.TextAlignment.Justify,
            PromptinoTextAlignment.Left => Avalonia.Media.TextAlignment.Left,
            _ => Avalonia.Media.TextAlignment.Left
        };
        _scriptScaleTransform.ScaleX = horizontalMirror ? -1 : 1;
        Opacity = Math.Clamp(opacity, ReadingPreferences.MinOpacity, ReadingPreferences.MaxOpacity);
        RootBorder.Padding = new Thickness(readingMargin);
        _snapProgressOnNextLayout = true;

        var baseColor = Color.Parse("#14B8A6");

        if (!string.IsNullOrWhiteSpace(textColor) && Color.TryParse(textColor, out var parsedColor))
        {
            baseColor = parsedColor;
        }

        ScriptTextBlock.Foreground = new SolidColorBrush(baseColor);
        RootBorder.Background = string.IsNullOrWhiteSpace(backgroundColor)
            ? new SolidColorBrush(Color.Parse("#141B22"))
            : Color.TryParse(backgroundColor, out var bgColor)
                ? new SolidColorBrush(bgColor)
                : new SolidColorBrush(Color.Parse("#141B22"));

        ReadingGuideFocusLine.Background = new SolidColorBrush(baseColor);
        ReadingGuideHighlightBand.Background = new SolidColorBrush(Color.FromArgb(0x22, baseColor.R, baseColor.G, baseColor.B));

        ReadingGuideFocusLine.IsVisible = readingGuide == ReadingGuideMode.Line || readingGuide == ReadingGuideMode.Both;
        ReadingGuideHighlightBand.IsVisible = readingGuide == ReadingGuideMode.HighlightBand || readingGuide == ReadingGuideMode.Both;
    }

    private void RefreshScrollableHeight(bool snapToTarget, bool clearPendingSnap)
    {
        if (_scrollMode == PrompterScrollMode.Basic)
        {
            if (snapToTarget)
                ApplyBasicScrollOffset(_scrollSmoother.Ratio);

            if (clearPendingSnap)
                _snapProgressOnNextLayout = false;

            return;
        }

        var max = ScriptScrollViewer.Extent.Height - ScriptScrollViewer.Viewport.Height;
        _scrollSmoother.SetScrollableHeight(max, snapToTarget);
        ApplyVisualScrollOffset();

        if (clearPendingSnap)
            _snapProgressOnNextLayout = false;
    }

    private void ApplyVisualScrollOffset()
    {
        if (_scrollMode == PrompterScrollMode.Basic) return;

        if (ScriptScrollViewer.Offset != default)
            ScriptScrollViewer.Offset = default;

        _scriptTranslateTransform.Y = -_scrollSmoother.CurrentOffset;
    }

    private void ApplyBasicScrollOffset(double ratio)
    {
        _scriptTranslateTransform.Y = 0;
        var max = ScriptScrollViewer.Extent.Height - ScriptScrollViewer.Viewport.Height;
        ScriptScrollViewer.Offset = max <= 0
            ? default
            : new Vector(0, max * ratio);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !_isResizing && !IsPointerOverResizeGrip(e))
        {
            BeginMoveDrag(e);
        }
    }

    private bool IsPointerOverResizeGrip(PointerPressedEventArgs e)
    {
        var source = e.Source as StyledElement;
        while (source is not null)
        {
            if (ReferenceEquals(source, ResizeGrip)) return true;
            source = source.Parent;
        }
        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsActive) return;

        if (e.Key == PlaybackToggleKey)
        {
            PlayPauseRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            ResetRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            SpeedDeltaRequested?.Invoke(5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            SpeedDeltaRequested?.Invoke(-5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.PageDown)
        {
            NextMarkerRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.PageUp)
        {
            PrevMarkerRequested?.Invoke();
            e.Handled = true;
        }
    }

    public void SetPlaybackState(bool isPlaying)
    {
        PausedIndicatorPill.IsVisible = !isPlaying;
    }

    private void UpdateFocusHint()
    {
        var focused = IsActive;
        FocusHintTextBlock.Text = focused
            ? $"Prompter focused: {PlaybackToggleKey} Play/Pause, R Reset, Up/Down speed, PgUp/PgDn markers"
            : "Click prompter to enable keyboard controls";
        RootBorder.BorderBrush = focused
            ? Application.Current?.FindResource("PromptinoBorderFocused") as Avalonia.Media.IBrush ?? RootBorder.BorderBrush
            : Application.Current?.FindResource("PromptinoBorderUnfocused") as Avalonia.Media.IBrush ?? RootBorder.BorderBrush;
        ResizeGrip.Opacity = focused ? 0.9 : 0.6;
    }
}
