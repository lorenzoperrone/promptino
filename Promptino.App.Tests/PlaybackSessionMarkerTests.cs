using FluentAssertions;
using Promptino.Core.Playback;
using System.Collections.Generic;

namespace Promptino.App.Tests;

public class PlaybackSessionMarkerTests
{
    private static readonly string Text = "one two three four five six seven eight nine ten"; // 10 words
    
    [Fact]
    public void SetMarkers_OrdersByOrderProperty()
    {
        var session = new PlaybackSession(Text);
        session.SetMarkers(new[]
        {
            new ScriptMarker("m2", "Middle", 2, 0.5),
            new ScriptMarker("m1", "Start", 1, 0.1),
            new ScriptMarker("m3", "End", 3, 0.9)
        });

        session.Markers.Should().HaveCount(3);
        session.Markers[0].Id.Should().Be("m1");
        session.Markers[1].Id.Should().Be("m2");
        session.Markers[2].Id.Should().Be("m3");
    }

    [Fact]
    public void JumpToNextMarker_JumpsToFirstMarker_WhenAtStart()
    {
        var session = new PlaybackSession(Text);
        session.SetMarkers(new[]
        {
            new ScriptMarker("m1", "Start", 1, 0.2),
            new ScriptMarker("m2", "Middle", 2, 0.5)
        });

        session.JumpToNextMarker();
        
        session.GetProgressRatio().Should().BeApproximately(0.2, 0.0001);
    }

    [Fact]
    public void JumpToNextMarker_JumpsToNext_WhenPastFirstMarker()
    {
        var session = new PlaybackSession(Text);
        session.SetMarkers(new[]
        {
            new ScriptMarker("m1", "Start", 1, 0.2),
            new ScriptMarker("m2", "Middle", 2, 0.5)
        });

        session.SetProgress(0.25);
        session.JumpToNextMarker();
        
        session.GetProgressRatio().Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public void JumpToPreviousMarker_JumpsToPrevious_WhenPastMultiple()
    {
        var session = new PlaybackSession(Text);
        session.SetMarkers(new[]
        {
            new ScriptMarker("m1", "Start", 1, 0.2),
            new ScriptMarker("m2", "Middle", 2, 0.5)
        });

        session.SetProgress(0.6);
        session.JumpToPreviousMarker();
        
        session.GetProgressRatio().Should().BeApproximately(0.5, 0.0001);
        
        session.JumpToPreviousMarker();
        session.GetProgressRatio().Should().BeApproximately(0.2, 0.0001);
    }

    [Fact]
    public void JumpToPreviousMarker_JumpsToStart_WhenBeforeFirstMarker()
    {
        var session = new PlaybackSession(Text);
        session.SetMarkers(new[]
        {
            new ScriptMarker("m1", "Start", 1, 0.2),
            new ScriptMarker("m2", "Middle", 2, 0.5)
        });

        session.SetProgress(0.1);
        session.JumpToPreviousMarker();
        
        session.GetProgressRatio().Should().Be(0);
    }
}
