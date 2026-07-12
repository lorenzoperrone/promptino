using System;
using System.Collections.Generic;
using System.Linq;

namespace Promptino.Core.Playback;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Completed
}

public sealed class PlaybackSession
{
    private readonly int _totalWords;
    private readonly List<ScriptMarker> _markers = [];

    public PlaybackSession(string text, int? preferredWpm = null)
    {
        _totalWords = CountWords(text);
        Wpm = ReadingSpeed.Clamp(preferredWpm ?? ReadingSpeed.DefaultWpm);
    }

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public int Wpm { get; private set; }
    public double ProgressWords { get; private set; }
    public bool HasScript => _totalWords > 0;
    public bool IsComplete => ProgressWords >= _totalWords;
    public IReadOnlyList<ScriptMarker> Markers => _markers;

    public bool CanPlay => HasScript && (State == PlaybackState.Stopped || State == PlaybackState.Paused) && !IsComplete;
    public bool CanPause => State == PlaybackState.Playing;
    public bool CanReset => ProgressWords > 0 || State == PlaybackState.Playing || State == PlaybackState.Completed;

    public bool TryPlay()
    {
        if (!CanPlay) return false;
        State = PlaybackState.Playing;
        return true;
    }

    public bool TryPause()
    {
        if (!CanPause) return false;
        State = PlaybackState.Paused;
        return true;
    }

    public void SetWpm(int wpm)
    {
        Wpm = ReadingSpeed.Clamp(wpm);
    }

    public void Reset()
    {
        ProgressWords = 0;
        State = PlaybackState.Stopped;
    }

    public void Advance(TimeSpan elapsed)
    {
        if (State != PlaybackState.Playing || !HasScript || IsComplete) return;
        ProgressWords = Math.Min(_totalWords, ProgressWords + (ReadingSpeed.WordsPerSecond(Wpm) * elapsed.TotalSeconds));
        if (IsComplete)
        {
            // Distinct from manually-paused: Completed signals the script reached the end on its own.
            // Reset() must be called before another playback can start.
            State = PlaybackState.Completed;
        }
    }

    public double GetProgressRatio()
    {
        return _totalWords == 0 ? 0 : ProgressWords / _totalWords;
    }

    public void SetProgress(double ratio)
    {
        ratio = Math.Clamp(ratio, 0.0, 1.0);
        ProgressWords = ratio * _totalWords;
        
        if (State == PlaybackState.Completed && ProgressWords < _totalWords)
        {
            State = PlaybackState.Paused;
        }
    }

    public void SetMarkers(IEnumerable<ScriptMarker> markers)
    {
        _markers.Clear();
        _markers.AddRange(markers.OrderBy(m => m.Order));
    }

    public void JumpToNextMarker()
    {
        var currentRatio = GetProgressRatio();
        var next = _markers.FirstOrDefault(m => m.ProgressRatio > currentRatio + 0.001);
        if (next != null)
        {
            SetProgress(next.ProgressRatio);
        }
    }

    public void JumpToPreviousMarker()
    {
        var currentRatio = GetProgressRatio();
        var prev = _markers.LastOrDefault(m => m.ProgressRatio < currentRatio - 0.001);
        if (prev != null)
        {
            SetProgress(prev.ProgressRatio);
        }
        else if (currentRatio > 0.001)
        {
            // If no previous marker but we're past start, jump to start
            SetProgress(0);
        }
    }

    private static int CountWords(string text)
    {
        return WordCounter.Count(text);
    }
}
