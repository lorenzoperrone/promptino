namespace Promptino.Core.Playback;

/// <summary>
/// Defines constants and utility functions for teleprompter reading speed (Words Per Minute).
/// </summary>
public static class ReadingSpeed
{
    /// <summary>Minimum allowed Words Per Minute (WPM).</summary>
    public const int MinWpm = 20;

    /// <summary>Maximum allowed Words Per Minute (WPM).</summary>
    public const int MaxWpm = 500;

    /// <summary>Default Words Per Minute (WPM) setting for comfortable reading.</summary>
    public const int DefaultWpm = 130;

    /// <summary>
    /// Clamps the specified WPM value within valid range bounds (<see cref="MinWpm"/> to <see cref="MaxWpm"/>).
    /// </summary>
    /// <param name="wpm">The target WPM value.</param>
    /// <returns>The clamped WPM value.</returns>
    public static int Clamp(int wpm) => Math.Clamp(wpm, MinWpm, MaxWpm);

    /// <summary>
    /// Calculates words scrolled per second for a given WPM rate.
    /// </summary>
    /// <param name="wpm">The Words Per Minute speed.</param>
    /// <returns>The number of words per second as a double precision value.</returns>
    public static double WordsPerSecond(int wpm) => Clamp(wpm) / 60d;
}
