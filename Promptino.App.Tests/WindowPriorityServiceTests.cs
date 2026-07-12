using Promptino.Platform;

namespace Promptino.App.Tests;

public class WindowPriorityServiceTests
{
    [Fact]
    public void TrySetAlwaysOnTop_ReturnsTrue_WhenApplyDelegateSucceeds()
    {
        var service = new WindowPriorityService();
        bool? appliedValue = null;

        var ok = service.TrySetAlwaysOnTop(value => appliedValue = value, enabled: true, out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        appliedValue.Should().BeTrue();
    }

    [Fact]
    public void TrySetAlwaysOnTop_PassesDisabledValue_WhenEnabledFalse()
    {
        var service = new WindowPriorityService();
        bool? appliedValue = null;

        var ok = service.TrySetAlwaysOnTop(value => appliedValue = value, enabled: false, out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        appliedValue.Should().BeFalse();
    }

    [Fact]
    public void TrySetAlwaysOnTop_ReturnsFalse_WithWarning_WhenApplyDelegateThrows()
    {
        // Simulates the platform layer failing (e.g. Avalonia API unavailable in some environment).
        // The service must swallow and surface a narrow, user-friendly warning.
        var service = new WindowPriorityService();

        var ok = service.TrySetAlwaysOnTop(_ => throw new InvalidOperationException("simulated"), enabled: true, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        warning.Should().Contain("always-on-top");
    }

    [Fact]
    public void TrySetScreenShareSafeMode_ReturnsFalse_WhenHandleMissing()
    {
        var service = new WindowPriorityService();

        var ok = service.TrySetScreenShareSafeMode(0, enabled: true, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
    }
}
