using FluentAssertions;
using Promptino.Core.Playback;

namespace Promptino.App.Tests;

public class PlaybackSessionTests
{
    [Fact]
    public void UsesCanonicalDefaultWpmWhenPreferenceMissing()
    {
        // Default WPM must come from the single source of truth (ReadingSpeed.DefaultWpm)
        // so PlaybackSession and the rest of the app cannot disagree.
        var session = new PlaybackSession("one two three");
        session.Wpm.Should().Be(ReadingSpeed.DefaultWpm);
    }

    [Fact]
    public void PlayPauseResetTransitionsAreStable()
    {
        var session = new PlaybackSession("one two three four five");
        session.TryPlay().Should().BeTrue();
        session.Advance(TimeSpan.FromSeconds(1));
        session.ProgressWords.Should().BeGreaterThan(0);

        session.TryPause().Should().BeTrue();
        var before = session.ProgressWords;
        session.Advance(TimeSpan.FromSeconds(1));
        session.ProgressWords.Should().Be(before);

        session.Reset();
        session.ProgressWords.Should().Be(0);
        session.State.Should().Be(PlaybackState.Stopped);
    }

    [Fact]
    public void EmptyScriptCannotPlay()
    {
        var session = new PlaybackSession("");
        session.CanPlay.Should().BeFalse();
        session.TryPlay().Should().BeFalse();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData("")]
    public void WhitespaceOnlyScriptCannotPlay(string text)
    {
        var session = new PlaybackSession(text);
        session.HasScript.Should().BeFalse();
        session.CanPlay.Should().BeFalse();
        session.TryPlay().Should().BeFalse();
    }

    [Fact]
    public void LiveSpeedChangeKeepsPositionAndUpdatesRate()
    {
        var session = new PlaybackSession("one two three four five six seven eight nine ten", 100);
        session.TryPlay();
        session.Advance(TimeSpan.FromSeconds(1));
        var before = session.ProgressWords;

        session.SetWpm(200);
        session.Advance(TimeSpan.FromSeconds(1));
        var delta = session.ProgressWords - before;

        delta.Should().BeApproximately(ReadingSpeed.WordsPerSecond(200), 0.05);
        before.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CanReset_IsFalse_WhenStoppedAtStart()
    {
        var session = new PlaybackSession("one two three");
        session.CanReset.Should().BeFalse();
    }

    [Fact]
    public void CanReset_IsTrue_WhenPlaying()
    {
        var session = new PlaybackSession("one two three");
        session.TryPlay();
        session.CanReset.Should().BeTrue();
    }

    [Fact]
    public void CanReset_IsTrue_AfterAdvancing()
    {
        var session = new PlaybackSession("one two three four five");
        session.TryPlay();
        session.Advance(TimeSpan.FromSeconds(1));
        session.TryPause();
        session.CanReset.Should().BeTrue();
    }

    [Fact]
    public void CanReset_IsFalse_WhenPausedAtStart_WithNoProgress()
    {
        // Paused with zero progress should not offer reset — nothing to undo
        var session = new PlaybackSession("one two three");
        session.TryPlay();
        session.TryPause();
        // No Advance called — ProgressWords still 0
        session.ProgressWords.Should().Be(0);
        session.CanReset.Should().BeFalse();
    }

    [Fact]
    public void State_BecomesCompleted_WhenScriptReachesEnd()
    {
        // Distinguishing a naturally-finished playback from a user-paused one is useful
        // for future UX decisions (e.g. showing "End of script" vs "Paused").
        var session = new PlaybackSession("one two three", ReadingSpeed.MaxWpm);
        session.TryPlay();
        // Advance long enough to definitely cross the end of the 3-word script.
        session.Advance(TimeSpan.FromSeconds(60));

        session.IsComplete.Should().BeTrue();
        session.State.Should().Be(PlaybackState.Completed);
        session.CanPlay.Should().BeFalse("a finished script must be reset before replaying");
        session.CanPause.Should().BeFalse();
        session.CanReset.Should().BeTrue("the user must be able to reset a finished script");
    }

    [Fact]
    public void Completed_ResetReturnsToStopped()
    {
        var session = new PlaybackSession("one two", ReadingSpeed.MaxWpm);
        session.TryPlay();
        session.Advance(TimeSpan.FromSeconds(60));
        session.State.Should().Be(PlaybackState.Completed);

        session.Reset();

        session.State.Should().Be(PlaybackState.Stopped);
        session.ProgressWords.Should().Be(0);
        session.CanPlay.Should().BeTrue();
    }

    [Fact]
    public void LongScript_CountsAndAdvancesWithinPerformanceBudget()
    {
        // Smoke test: a very long script must not block the UI for noticeable time on construction.
        var words = string.Join(' ', Enumerable.Repeat("word", 100_000));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var session = new PlaybackSession(words);
        session.TryPlay();
        session.Advance(TimeSpan.FromSeconds(0.1));

        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            "constructing and stepping a 100k-word session should be near-instant on a normal machine");
        session.HasScript.Should().BeTrue();
    }
}
