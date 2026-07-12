using System.Xml.Linq;

namespace Promptino.App.Tests;

public class MainWindowXamlTests
{
    private static readonly XDocument Xaml = XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Promptino.App", "MainWindow.axaml"));

    private static readonly XNamespace Av = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainWindow_ShouldHaveCanResizeTrue()
    {
        var window = Xaml.Root!;
        window.Attribute("CanResize")?.Value.Should().Be("True");
    }

    [Fact]
    public void MainWindow_ShouldHavePracticalMinDimensions()
    {
        var window = Xaml.Root!;
        int.Parse(window.Attribute("MinWidth")!.Value).Should().BeGreaterThanOrEqualTo(400);
        int.Parse(window.Attribute("MinHeight")!.Value).Should().BeGreaterThanOrEqualTo(300);
    }

    [Fact]
    public void MainWindow_ShouldContainScrollViewer()
    {
        var scrollViewer = Xaml.Descendants(Av + "ScrollViewer").FirstOrDefault();
        scrollViewer.Should().NotBeNull("control panel body must be wrapped in a ScrollViewer");
    }

    [Fact]
    public void ScrollViewer_ShouldHaveVerticalScrollingEnabled()
    {
        var scrollViewer = Xaml.Descendants(Av + "ScrollViewer").First();
        var vis = scrollViewer.Attribute("VerticalScrollBarVisibility")?.Value;
        vis.Should().BeOneOf(new[] { "Auto", "Visible" },
            "vertical scrolling must be available for content taller than the window");
    }

    [Fact]
    public void ScrollViewer_ShouldDisableHorizontalScrolling()
    {
        var scrollViewer = Xaml.Descendants(Av + "ScrollViewer").First();
        var vis = scrollViewer.Attribute("HorizontalScrollBarVisibility")?.Value;
        vis.Should().BeOneOf(new[] { "Disabled", "Hidden", null },
            "horizontal scrolling is not needed and should not appear");
    }

    [Fact]
    public void MainWindow_ShouldExposeOpacitySlider()
    {
        var slider = Xaml.Descendants(Av + "Slider")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "WindowOpacitySlider");
        slider.Should().NotBeNull("opacity slider must be present and reachable by scrolling");
    }

    [Fact]
    public void MainWindow_ShouldExposeSpeedSlider()
    {
        var slider = Xaml.Descendants(Av + "Slider")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "SpeedSlider");
        slider.Should().NotBeNull("speed slider must be present in the control panel");
    }

    [Fact]
    public void MainWindow_ShouldExposeLoadScriptButton()
    {
        var button = Xaml.Descendants(Av + "Button")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "LoadScriptButton");
        button.Should().NotBeNull("Load Script button must be immediately usable at default size");
    }

    [Fact]
    public void MainWindow_ShouldExposeHotkeyAndProfileSections()
    {
        var hotkeyBox = Xaml.Descendants(Av + "TextBox")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "HotkeyTextBox");
        var profileBox = Xaml.Descendants(Av + "TextBox")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "ProfileNameTextBox");

        hotkeyBox.Should().NotBeNull("hotkey section must be present and scrollable to");
        profileBox.Should().NotBeNull("profiles section must be present and scrollable to");
    }

    [Fact]
    public void MainWindow_ShouldExposeCleanupPreviewWorkflow()
    {
        var previewButton = Xaml.Descendants(Av + "Button")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "PreviewCleanupButton");
        var applyButton = Xaml.Descendants(Av + "Button")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "ApplyCleanupButton");
        var previewPanel = Xaml.Descendants(Av + "Grid")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "CleanupPreviewPanel");

        previewButton.Should().NotBeNull("cleanup preview must be refreshable from the control panel");
        applyButton.Should().NotBeNull("cleaned text must be explicitly applicable to the prompter");
        previewPanel.Should().NotBeNull("before/after cleanup preview must be visible in the control panel");
    }

    [Fact]
    public void MainWindow_ShouldExposePlaybackSmoothnessModeSelector()
    {
        var comboBox = Xaml.Descendants(Av + "ComboBox")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "PlaybackSmoothnessModeComboBox");

        comboBox.Should().NotBeNull("smooth scrolling mode must be selectable from the control panel");
    }

    [Fact]
    public void MainWindow_ShouldExposeActivePlaybackDriverStatus()
    {
        var status = Xaml.Descendants(Av + "TextBlock")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "PlaybackDriverStatusTextBlock");

        status.Should().NotBeNull("render-aligned fallback behavior must be visible to the user");
        status!.Attribute("Text")?.Value.Should().Contain("Active driver");
    }

    [Fact]
    public void MainWindow_ShouldExposeTextScrollingPerformanceToggle()
    {
        var toggle = Xaml.Descendants(Av + "CheckBox")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "HighPerformanceTextScrollingCheckBox");

        toggle.Should().NotBeNull("users must be able to switch between basic and high-performance text scrolling");
        toggle!.Attribute("Content")?.Value.Should().Contain("ChkHighPerformance");
    }

    [Fact]
    public void MainWindow_ShouldExposeEditScriptButton()
    {
        var button = Xaml.Descendants(Av + "Button")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "EditScriptButton");
        button.Should().NotBeNull("Edit Script button must be present in the control panel");
    }

    [Fact]
    public void MainWindow_ShouldExposeExternalEditorConfiguration()
    {
        var textBox = Xaml.Descendants(Av + "TextBox")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "ExternalEditorPathTextBox");
        var button = Xaml.Descendants(Av + "Button")
            .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == "ApplyExternalEditorPathButton");

        textBox.Should().NotBeNull("External Editor Path textbox must be present");
        button.Should().NotBeNull("Apply External Editor Path button must be present");
    }
}
