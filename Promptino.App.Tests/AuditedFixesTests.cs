using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Promptino.Core.Playback;
using Promptino.Core.Scripts;
using Promptino.Storage;
using Xunit;

namespace Promptino.App.Tests;

public class AuditedFixesTests
{
    [Fact]
    public void PlaybackSession_ScrubbingTo100Percent_TransitionsToCompleted()
    {
        var session = new PlaybackSession("Hello world this is a test script for teleprompter reading.");
        session.TryPlay().Should().BeTrue();
        session.State.Should().Be(PlaybackState.Playing);

        session.SetProgress(1.0);

        session.State.Should().Be(PlaybackState.Completed);
        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void PlaybackSession_CanReset_ReturnsTrueWhenPausedAtZero()
    {
        var session = new PlaybackSession("Testing reset functionality.");
        session.TryPlay().Should().BeTrue();
        session.TryPause().Should().BeTrue();
        session.State.Should().Be(PlaybackState.Paused);
        session.ProgressWords.Should().Be(0);

        session.CanReset.Should().BeTrue();
        session.Reset();
        session.State.Should().Be(PlaybackState.Stopped);
    }

    [Fact]
    public void PlaybackSession_NegativeTimeDelta_DoesNotScrollBackwards()
    {
        var session = new PlaybackSession("One two three four five six seven eight nine ten.");
        session.TryPlay().Should().BeTrue();
        session.Advance(TimeSpan.FromSeconds(5));
        var progressBefore = session.ProgressWords;
        progressBefore.Should().BeGreaterThan(0);

        session.Advance(TimeSpan.FromSeconds(-10));
        session.ProgressWords.Should().Be(progressBefore);
    }

    [Fact]
    public void ScriptTextTransformer_UnclosedFrontMatter_DoesNotBlankScript()
    {
        var rawText = "---\nTitle: Test Script\nThis text must not be erased when front matter is unclosed.";
        var transformer = new ScriptTextTransformer();

        var result = transformer.Transform(rawText, ".md");

        result.Should().Contain("This text must not be erased when front matter is unclosed.");
    }

    [Fact]
    public void ScriptTextTransformer_UnclosedFencedCodeBlock_DoesNotBlankScript()
    {
        var rawText = "Introduction line\n```\nsome code line without closing fence";
        var transformer = new ScriptTextTransformer();

        var result = transformer.Transform(rawText, ".md");

        result.Should().Contain("Introduction line");
        result.Should().Contain("some code line without closing fence");
    }

    [Fact]
    public async Task IoRetry_WriteTextWriteThroughAsync_WritesTextSuccessfully()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"promptino_writethrough_test_{Guid.NewGuid():N}.tmp");
        try
        {
            await IoRetry.WriteTextWriteThroughAsync(tempFile, "WriteThrough test content", default);
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Be("WriteThrough test content");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
