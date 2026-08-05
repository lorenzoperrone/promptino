using System;

namespace Promptino.App;

public sealed class PrompterScrollSmoother
{
    public const double DefaultDamping = 0.25;

    private readonly double _damping;

    public PrompterScrollSmoother(double damping = DefaultDamping)
    {
        if (damping <= 0 || damping > 1)
            throw new ArgumentOutOfRangeException(nameof(damping), "Damping must be greater than 0 and no more than 1.");

        _damping = damping;
    }

    public double Ratio { get; private set; }
    public double ScrollableHeight { get; private set; }
    public double TargetOffset { get; private set; }
    public double CurrentOffset { get; private set; }

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
