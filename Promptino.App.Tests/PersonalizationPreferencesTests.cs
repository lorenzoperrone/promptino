using Promptino.Storage.Settings;

namespace Promptino.App.Tests;

public class PersonalizationPreferencesTests
{
    [Fact]
    public async Task Defaults_AreApplied_WhenSetupSkippedOrMissing()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        await vm.InitializeAsync();

        vm.TextSize.Should().Be(ReadingPreferences.Defaults.TextSize);
        vm.FontFamily.Should().Be(ReadingPreferences.Defaults.FontFamily);
        vm.ReadingMargin.Should().Be(ReadingPreferences.Defaults.ReadingMargin);
    }

    [Fact]
    public async Task ConfirmCalibration_PersistsPersonalizationAndAllowsLaterEditing()
    {
        var path = TestHelpers.TempPath();
        var vm = TestHelpers.CreateVm(path);
        await vm.InitializeAsync();

        vm.SetPersonalization(36, 1.5, 0.9, true, "Calibri", 84);
        vm.SetCalibrationWpm(165);
        await vm.ConfirmCalibrationAsync();

        var vmReloaded = TestHelpers.CreateVm(path);
        await vmReloaded.InitializeAsync();
        vmReloaded.TextSize.Should().Be(36);
        vmReloaded.AlwaysOnTop.Should().BeTrue();

        vmReloaded.SetPersonalization(34, 1.4, 0.92, false, "Arial", 80);
        await vmReloaded.ConfirmCalibrationAsync();

        var vmEdited = TestHelpers.CreateVm(path);
        await vmEdited.InitializeAsync();
        vmEdited.TextSize.Should().Be(34);
        vmEdited.FontFamily.Should().Be("Arial");
    }

    [Fact]
    public async Task PartialOrCorruptSettings_FallsBackSafely()
    {
        var path = TestHelpers.TempPath();
        await File.WriteAllTextAsync(path, "{not-json");

        var vm = TestHelpers.CreateVm(path);
        await vm.InitializeAsync();

        vm.TextSize.Should().Be(ReadingPreferences.Defaults.TextSize);
        vm.IsCalibrationRecommended.Should().BeTrue();
        vm.SettingsRecoveryMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AlwaysOnTopFailure_ShowsGracefulWarning()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());
        vm.NotifyAlwaysOnTopApplyFailure();
        vm.WindowBehaviorWarning.Should().Contain("saved");
    }

    [Theory]
    [InlineData(-1, 1.3, 0.95, 72)]
    [InlineData(0, 1.3, 0.95, 72)]
    [InlineData(200, 1.3, 0.95, 72)]
    [InlineData(32, 1.3, -0.1, 72)]
    [InlineData(32, 1.3, 1.5, 72)]
    [InlineData(32, 1.3, 0.95, -10)]
    public void SetPersonalization_ClampsOutOfRangeValues(int textSize, double lineSpacing, double opacity, int margin)
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        vm.SetPersonalization(textSize, lineSpacing, opacity, false, "Segoe UI", margin);

        vm.TextSize.Should().BeInRange(ReadingPreferences.MinTextSize, ReadingPreferences.MaxTextSize);
        vm.WindowOpacity.Should().BeInRange(ReadingPreferences.MinOpacity, ReadingPreferences.MaxOpacity);
        vm.ReadingMargin.Should().BeGreaterThanOrEqualTo(ReadingPreferences.MinReadingMargin);
    }
}
