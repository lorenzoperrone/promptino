using System.Text.RegularExpressions;

namespace Promptino.App.Tests;

public class PlaybackLoopSourceTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string MainWindowSource = File.ReadAllText(Path.Combine(RepositoryRoot, "Promptino.App", "MainWindow.axaml.cs"));

    [Fact]
    public void PlaybackLoop_ShouldUsePublicCompositionApi()
    {
        MainWindowSource.Should().Contain("ElementComposition.GetElementVisual");
        MainWindowSource.Should().Contain("RequestCompositionUpdate");
        MainWindowSource.Should().NotContain("BindingFlags");
        MainWindowSource.Should().NotContain("IRendererWithCompositor");
    }

    [Fact]
    public void PlaybackLoop_ShouldUseMonotonicStopwatchTiming()
    {
        MainWindowSource.Should().Contain("Stopwatch.GetTimestamp()");
        MainWindowSource.Should().Contain("Stopwatch.GetElapsedTime");
        MainWindowSource.Should().NotContain("DateTime.UtcNow");
    }

    [Fact]
    public void PlaybackLoop_ShouldScheduleNextCompositionFrameBeforeAdvancing()
    {
        var body = ExtractMethodBody("OnCompositionFrame");

        body.IndexOf("ScheduleCompositionFrame();", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("AdvancePlaybackFrame", StringComparison.Ordinal));
    }

    [Fact]
    public void PlaybackLoop_ShouldUpdateButtonsOnlyWhenStateChanges()
    {
        var body = ExtractMethodBody("AdvancePlaybackFrame");

        body.Should().Contain("previousState");
        body.Should().Contain("if (_playbackSession.State != previousState)");
        Regex.Matches(body, "UpdatePlaybackButtons\\(").Count.Should().Be(1);
    }

    private static string ExtractMethodBody(string methodName)
    {
        var signature = $"private void {methodName}";
        var start = MainWindowSource.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"{methodName} must exist");

        var openBrace = MainWindowSource.IndexOf('{', start);
        var depth = 0;
        for (var i = openBrace; i < MainWindowSource.Length; i++)
        {
            if (MainWindowSource[i] == '{') depth++;
            if (MainWindowSource[i] == '}') depth--;
            if (depth == 0) return MainWindowSource[openBrace..(i + 1)];
        }

        throw new InvalidOperationException($"Could not parse {methodName} body.");
    }
}
