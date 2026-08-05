using System;

namespace Promptino.App;

/// <summary>
/// Provides exponential smoothing (linear interpolation with damping) for teleprompter scrolling to eliminate visual jitter.
/// </summary>
public sealed class PrompterScrollSmoother
{
    /// <summary>Default damping factor for smooth interpolation (0.25).</summary>
    public const double DefaultDamping = 0.25;

    private readonly double _damping;

    /// <summary>
    /// Initializes a new instance of <see cref="PrompterScrollSmoother"/> with the specified damping coefficient.
    /// </summary>
    /// <param name="damping">Interpolation damping coefficient between 0 (exclusive) and 1 (inclusive).</param>
    public PrompterScrollSmoother(double damping = DefaultDamping)
    {
        if (damping <= 0 || damping > 1)
            throw new ArgumentOutOfRangeException(nameof(damping), "Damping must be greater than 0 and no more than 1.");

        _damping = damping;
    }

    /// <summary>Gets current target normalized progress ratio (0.0 to 1.0).</summary>
    public double Ratio { get; private set; }

    /// <summary>Gets total scrollable height of the prompter content view in pixels.</summary>
    public double ScrollableHeight { get; private set; }

    /// <summary>Gets exact calculated target pixel offset based on ratio and height.</summary>
    public double TargetOffset { get; private set; }

    /// <summary>Gets current interpolated visual pixel offset.</summary>
    public double CurrentOffset { get; private set; }

    /// <summary>
    /// Sets total scrollable content height in pixels and updates target offset.
    /// </summary>
    /// <param name="height">Scrollable height in pixels.</param>
    /// <param name="snapToTarget">If <c>true</c>, immediately snaps current offset to target offset without damping.</param>
    /// <returns>The updated current pixel offset.</returns>
    public double SetScrollableHeight(double height, bool snapToTarget = false)
    {
        ScrollableHeight = NormalizeHeight(height);
        TargetOffset = ScrollableHeight * Ratio;

        if (snapToTarget || CurrentOffset > ScrollableHeight)
            CurrentOffset = TargetOffset;
        else
            CurrentOffset = Math.Clamp(CurrentOffset, 0, ScrollableHeight);

        return CurrentOffset;
    }

    /// <summary>
    /// Updates normalized progress ratio and advances interpolated current offset toward target offset.
    /// </summary>
    /// <param name="ratio">Target progress ratio (0.0 to 1.0).</param>
    /// <param name="snapToTarget">If <c>true</c>, immediately snaps offset without damping (e.g. on manual seek or reset).</param>
    /// <returns>The updated current pixel offset.</returns>
    public double UpdateProgress(double ratio, bool snapToTarget = false)
    {
        if (double.IsNaN(ratio)) ratio = 0;
        Ratio = Math.Clamp(ratio, 0, 1);
        TargetOffset = ScrollableHeight * Ratio;

        if (snapToTarget || Ratio <= 0 || Ratio >= 1 || ScrollableHeight <= 0)
            CurrentOffset = TargetOffset;
        else
            CurrentOffset += (TargetOffset - CurrentOffset) * _damping;

        return CurrentOffset;
    }

    /// <summary>
    /// Resets all position, ratio, and height state to zero.
    /// </summary>
    public void Reset()
    {
        Ratio = 0;
        ScrollableHeight = 0;
        TargetOffset = 0;
        CurrentOffset = 0;
    }

    private static double NormalizeHeight(double height)
        => double.IsFinite(height) ? Math.Max(0, height) : 0;
}
