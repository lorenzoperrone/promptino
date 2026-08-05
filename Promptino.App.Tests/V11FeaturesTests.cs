using System.Threading.Tasks;
using FluentAssertions;
using Promptino.App.Tests;
using Promptino.Core.Scripts;
using Promptino.Platform;
using Promptino.Storage.Settings;
using Xunit;

namespace Promptino.App.Tests;

public class V11FeaturesTests
{
    [Fact]
    public void ScriptCueParser_ParsesSpeakerAndStageDirectionsCorrectly()
    {
        var line = "[HOST]: Benvenuti tutti (pausa 2s) a questa presentazione [applausi]";

        var tokens = ScriptCueParser.ParseTokens(line);

        tokens.Should().HaveCount(5);
        tokens[0].Text.Should().Be("[HOST]: ");
        tokens[0].Type.Should().Be(CueTokenType.Speaker);

        tokens[1].Text.Should().Be("Benvenuti tutti ");
        tokens[1].Type.Should().Be(CueTokenType.Text);

        tokens[2].Text.Should().Be("(pausa 2s)");
        tokens[2].Type.Should().Be(CueTokenType.StageDirection);

        tokens[3].Text.Should().Be(" a questa presentazione ");
        tokens[3].Type.Should().Be(CueTokenType.Text);

        tokens[4].Text.Should().Be("[applausi]");
        tokens[4].Type.Should().Be(CueTokenType.StageDirection);
    }

    [Fact]
    public void NoOpWindowClickThroughService_ReturnsFalse()
    {
        var service = new NoOpWindowClickThroughService();
        var result = service.SetClickThrough(System.IntPtr.Zero, true);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AppSettingsStore_ShouldRoundTripV11Settings()
    {
        var settingsPath = TestHelpers.TempPath();
        var store = new AppSettingsStore(settingsPath);

        var original = new AppSettings(
            CalibrationCompleted: true,
            DefaultWpm: 140,
            Preferences: ReadingPreferences.Defaults,
            ClickThroughEnabled: true,
            TargetScreenIndex: 1,
            PrompterFullscreen: true,
            TargetPresentationMinutes: 15,
            ShowPresentationTimer: true);

        await store.SaveAsync(original);

        var load = await store.LoadAsync();
        load.Settings.EffectiveClickThroughEnabled.Should().BeTrue();
        load.Settings.EffectiveTargetScreenIndex.Should().Be(1);
        load.Settings.EffectivePrompterFullscreen.Should().BeTrue();
        load.Settings.EffectiveTargetPresentationMinutes.Should().Be(15);
        load.Settings.EffectiveShowPresentationTimer.Should().BeTrue();
    }
}
