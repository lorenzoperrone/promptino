using Promptino.Core.Playback;

namespace Promptino.App.Tests;

public class CalibrationFlowTests
{
    [Fact]
    public async Task Initialize_FirstRun_ShowsRecommendedCalibration()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        await vm.InitializeAsync();

        vm.IsCalibrationRecommended.Should().BeTrue();
        vm.IsCalibrationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SkipCalibration_DoesNotBlockReadyState()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        await vm.InitializeAsync();
        vm.SkipCalibration();

        vm.IsCalibrationVisible.Should().BeFalse();
        vm.StatusMessage.Should().Contain("skipped");
    }

    [Fact]
    public async Task ConfirmCalibration_SavesAndRestoresDefaultWpm()
    {
        var path = TestHelpers.TempPath();
        var vm = TestHelpers.CreateVm(path);

        await vm.InitializeAsync();
        vm.SetCalibrationWpm(170);
        await vm.ConfirmCalibrationAsync();

        var vmReloaded = TestHelpers.CreateVm(path);
        await vmReloaded.InitializeAsync();

        vmReloaded.CalibrationWpm.Should().Be(170);
        vmReloaded.IsCalibrationRecommended.Should().BeFalse();
    }

    [Fact]
    public async Task OpenCalibration_AfterSkip_MakesCalibrationVisibleAndRecommended()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        await vm.InitializeAsync();
        vm.SkipCalibration();
        vm.OpenCalibration();

        vm.IsCalibrationVisible.Should().BeTrue();
        vm.IsCalibrationRecommended.Should().BeTrue();
    }

    [Fact]
    public async Task OpenCalibration_AfterConfirm_AllowsRecalibration()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());

        await vm.InitializeAsync();
        vm.SetCalibrationWpm(150);
        await vm.ConfirmCalibrationAsync();
        vm.OpenCalibration();

        vm.IsCalibrationVisible.Should().BeTrue();
        vm.IsCalibrationRecommended.Should().BeTrue();
    }

    [Fact]
    public void ReadingSpeed_UsesPlaybackSemantics()
    {
        ReadingSpeed.WordsPerSecond(120).Should().BeApproximately(2.0, 0.001);
        ReadingSpeed.Clamp(20).Should().Be(ReadingSpeed.MinWpm);
    }
}
