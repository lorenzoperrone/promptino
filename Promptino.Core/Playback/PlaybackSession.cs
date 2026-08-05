using System;
using System.Collections.Generic;
using System.Linq;

namespace Promptino.Core.Playback;

/// <summary>
/// Defines the execution state of a teleprompter playback session.
/// </summary>
public enum PlaybackState
{
    /// <summary>Playback is stopped at position 0.</summary>
    Stopped,

    /// <summary>Playback is actively advancing based on elapsed time and WPM.</summary>
    Playing,

    /// <summary>Playback is paused at the current word position.</summary>
    Paused,

    /// <summary>Playback has reached the end of the script naturally.</summary>
    Completed
}

/// <summary>
/// Manages teleprompter session state, position tracking, speed controls, and marker navigation.
/// </summary>
public sealed class PlaybackSession
{
    private readonly int _totalWords;
    private readonly List<ScriptMarker> _markers = [];

    /// <summary>
    /// Initializes a new instance of <see cref="PlaybackSession"/> for the given script text.
    /// </summary>
    /// <param name="text">The raw or cleaned script text to scroll.</param>
    /// <param name="preferredWpm">Optional initial WPM rate. Uses <see cref="ReadingSpeed.DefaultWpm"/> if null.</param>
    public PlaybackSession(string text, int? preferredWpm = null)
    {
        _totalWords = CountWords(text);
        Wpm = ReadingSpeed.Clamp(preferredWpm ?? ReadingSpeed.DefaultWpm);
    }

    /// <summary>Gets the current playback state.</summary>
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    /// <summary>Gets the current target reading speed in Words Per Minute.</summary>
    public int Wpm { get; private set; }

    /// <summary>Gets the current progress measured in floating-point words scrolled.</summary>
    public double ProgressWords { get; private set; }

    /// <summary>Gets a value indicating whether the session contains non-empty script content.</summary>
    public bool HasScript => _totalWords > 0;

    /// <summary>Gets a value indicating whether progress has reached or exceeded total words.</summary>
    public bool IsComplete => ProgressWords >= _totalWords;

    /// <summary>Gets the read-only list of ordered navigation markers in the script.</summary>
    public IReadOnlyList<ScriptMarker> Markers => _markers;

    /// <summary>Gets a value indicating whether play can be initiated.</summary>
    public bool CanPlay => HasScript && (State == PlaybackState.Stopped || State == PlaybackState.Paused) && !IsComplete;

    /// <summary>Gets a value indicating whether active playback can be paused.</summary>
    public bool CanPause => State == PlaybackState.Playing;

    /// <summary>Gets a value indicating whether the playback session can be reset.</summary>
    public bool CanReset => ProgressWords > 0 || State == PlaybackState.Playing || State == PlaybackState.Completed || State == PlaybackState.Paused;

    /// <summary>
    /// Attempts to transition the playback state to <see cref="PlaybackState.Playing"/>.
    /// </summary>
    /// <returns><c>true</c> if playback started successfully; otherwise, <c>false</c>.</returns>
    public bool TryPlay()
    {
        if (!CanPlay) return false;
        State = PlaybackState.Playing;
        return true;
    }

    /// <summary>
    /// Attempts to transition the playback state to <see cref="PlaybackState.Paused"/>.
    /// </summary>
    /// <returns><c>true</c> if playback was paused successfully; otherwise, <c>false</c>.</returns>
    public bool TryPause()
    {
        if (!CanPause) return false;
        State = PlaybackState.Paused;
        return true;
    }

    /// <summary>
    /// Sets the reading speed in WPM, clamping the value within valid bounds.
    /// </summary>
    /// <param name="wpm">The requested WPM.</param>
    public void SetWpm(int wpm)
    {
        Wpm = ReadingSpeed.Clamp(wpm);
    }

    /// <summary>
    /// Resets playback progress to 0 and resets the state to <see cref="PlaybackState.Stopped"/>.
    /// </summary>
    public void Reset()
    {
        ProgressWords = 0;
        State = PlaybackState.Stopped;
    }

    /// <summary>
    /// Advances playback position based on the elapsed time delta and configured WPM rate.
    /// </summary>
    /// <param name="elapsed">The time duration that has passed since the last tick.</param>
    public void Advance(TimeSpan elapsed)
    {
        if (State != PlaybackState.Playing || !HasScript || IsComplete) return;
        var seconds = Math.Max(0.0, elapsed.TotalSeconds);
        ProgressWords = Math.Min(_totalWords, ProgressWords + (ReadingSpeed.WordsPerSecond(Wpm) * seconds));
        if (IsComplete)
        {
            // Distinct from manually-paused: Completed signals the script reached the end on its own.
            // Reset() must be called before another playback can start.
            State = PlaybackState.Completed;
        }
    }

    /// <summary>
    /// Returns the normalized progress ratio (0.0 to 1.0) of the current position relative to total words.
    /// </summary>
    /// <returns>A double precision value between 0.0 and 1.0.</returns>
    public double GetProgressRatio()
    {
        return _totalWords == 0 ? 0 : ProgressWords / _totalWords;
    }

    /// <summary>
    /// Sets the scroll progress ratio directly, clamping ratio between 0.0 and 1.0.
    /// </summary>
    /// <param name="ratio">Target progress ratio (0.0 to 1.0).</param>
    public void SetProgress(double ratio)
    {
        ratio = Math.Clamp(ratio, 0.0, 1.0);
        ProgressWords = ratio * _totalWords;
        
        if (IsComplete)
        {
            State = PlaybackState.Completed;
        }
        else if (State == PlaybackState.Completed && ProgressWords < _totalWords)
        {
            State = PlaybackState.Paused;
        }
    }

    /// <summary>
    /// Replaces active script markers with the provided collection, sorted sequentially by order.
    /// </summary>
    /// <param name="markers">The collection of script markers to set.</param>
    public void SetMarkers(IEnumerable<ScriptMarker> markers)
    {
        _markers.Clear();
        _markers.AddRange(markers.OrderBy(m => m.Order));
    }

    /// <summary>
    /// Jumps playback position to the next marker occurring after the current ratio.
    /// </summary>
    public void JumpToNextMarker()
    {
        var currentRatio = GetProgressRatio();
        var next = _markers.FirstOrDefault(m => m.ProgressRatio > currentRatio + 0.001);
        if (next != null)
        {
            SetProgress(next.ProgressRatio);
        }
    }

    /// <summary>
    /// Jumps playback position to the previous marker occurring before the current ratio, or position 0 if none exist.
    /// </summary>
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
