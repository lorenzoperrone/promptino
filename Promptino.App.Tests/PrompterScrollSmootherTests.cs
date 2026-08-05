using Promptino.App;

namespace Promptino.App.Tests;

public class PrompterScrollSmootherTests
{
    [Fact]
    public void UpdateProgress_LerpsTowardTargetWithoutChangingTarget()
    {
        var smoother = new PrompterScrollSmoother(damping: 0.25);
        smoother.SetScrollableHeight(400);

        var first = smoother.UpdateProgress(0.5);
        var second = smoother.UpdateProgress(0.5);

        smoother.TargetOffset.Should().Be(200);
        first.Should().Be(50);
        second.Should().Be(87.5);
        second.Should().BeGreaterThan(first);
        second.Should().BeLessThan(smoother.TargetOffset);
    }

    [Fact]
    public void UpdateProgress_SnapsWhenRequestedOrAtBoundaries()
    {
        var smoother = new PrompterScrollSmoother(damping: 0.25);
        smoother.SetScrollableHeight(300);

        smoother.UpdateProgress(0.4, snapToTarget: true).Should().Be(120);
        smoother.UpdateProgress(1).Should().Be(300);
        smoother.UpdateProgress(0).Should().Be(0);
    }

    [Fact]
    public void SetScrollableHeight_NormalizesInvalidOrNegativeValues()
    {
        var smoother = new PrompterScrollSmoother();

        smoother.SetScrollableHeight(double.NaN);
        smoother.ScrollableHeight.Should().Be(0);

        smoother.SetScrollableHeight(-10);
        smoother.ScrollableHeight.Should().Be(0);
    }

    [Fact]
    public void UpdateProgress_ClampsNanAndInfinity()
    {
        var smoother = new PrompterScrollSmoother(damping: 0.25);
        smoother.SetScrollableHeight(400);

        smoother.UpdateProgress(double.NaN);
        smoother.Ratio.Should().Be(0);
        smoother.CurrentOffset.Should().Be(0);

        smoother.UpdateProgress(double.PositiveInfinity);
        smoother.Ratio.Should().Be(1);

        smoother.UpdateProgress(double.NegativeInfinity);
        smoother.Ratio.Should().Be(0);
    }
}
