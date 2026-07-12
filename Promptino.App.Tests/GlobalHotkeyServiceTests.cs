using Promptino.Platform;

namespace Promptino.App.Tests;

public sealed class HotkeyModifiersTests
{
    [Theory]
    [InlineData(HotkeyModifiers.None, 0)]
    [InlineData(HotkeyModifiers.Alt, 1)]
    [InlineData(HotkeyModifiers.Control, 2)]
    [InlineData(HotkeyModifiers.Shift, 4)]
    [InlineData(HotkeyModifiers.Win, 8)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 3)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, 7)]
    public void Values_AreCorrectFlags(HotkeyModifiers value, int expected)
    {
        ((int)value).Should().Be(expected);
    }
}

public sealed class GlobalHotkeyTests
{
    [Fact]
    public void Default_UsesControlAltSpace()
    {
        var hk = GlobalHotkey.Default;
        hk.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hk.VirtualKey.Should().Be(0x20);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control, 0x08, true)]
    [InlineData(HotkeyModifiers.Control, 0xFE, true)]
    [InlineData(HotkeyModifiers.Control, 0x20, true)]
    [InlineData(HotkeyModifiers.None, 0x20, false)]
    [InlineData(HotkeyModifiers.Control, 0x00, false)]
    [InlineData(HotkeyModifiers.Control, 0x07, false)]
    [InlineData(HotkeyModifiers.None, 0x00, false)]
    public void IsValid_RejectsMissingModifierOrOutOfRangeKey(HotkeyModifiers mods, int vk, bool expected)
    {
        var hk = new GlobalHotkey(mods, vk);
        hk.IsValid.Should().Be(expected);
    }

    [Fact]
    public void Equality_UsesAllProperties()
    {
        var a = new GlobalHotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20);
        var b = new GlobalHotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20);
        var c = new GlobalHotkey(HotkeyModifiers.Control, 0x20);

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}

public sealed class HotkeyRegistrationResultTests
{
    [Fact]
    public void Ok_HasSuccessTrue()
    {
        var r = HotkeyRegistrationResult.Ok();
        r.Success.Should().BeTrue();
        r.IsConflict.Should().BeFalse();
        r.Warning.Should().BeNull();
    }

    [Fact]
    public void Conflict_HasIsConflictTrue()
    {
        var r = HotkeyRegistrationResult.Conflict();
        r.Success.Should().BeFalse();
        r.IsConflict.Should().BeTrue();
        r.Warning.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Failure_HasWarning()
    {
        var r = HotkeyRegistrationResult.Failure("nope");
        r.Success.Should().BeFalse();
        r.IsConflict.Should().BeFalse();
        r.Warning.Should().Be("nope");
    }
}

public sealed class NoOpGlobalHotkeyServiceTests
{
    [Fact]
    public void UpdateHotkeys_ReturnsFailure()
    {
        var svc = new NoOpGlobalHotkeyService();
        var result = svc.UpdateHotkeys([(1, GlobalHotkey.Default)]);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Stop_DoesNotThrow()
    {
        var svc = new NoOpGlobalHotkeyService();
        svc.Stop();
        // No exception = pass
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var svc = new NoOpGlobalHotkeyService();
        svc.Dispose();
    }

    [Fact]
    public void HotkeyPressed_CanBeSubscribedAndUnsubscribed()
    {
        var svc = new NoOpGlobalHotkeyService();
        Action<int> handler = _ => { };
        svc.HotkeyPressed += handler;
        svc.HotkeyPressed -= handler;
    }
}
