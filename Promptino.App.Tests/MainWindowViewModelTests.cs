using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Promptino.App.ViewModels;
using Promptino.App.Services;
using Promptino.Core.Scripts;
using FluentAssertions;
using Xunit;

namespace Promptino.App.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void PrimaryActions_ShouldExposeOnlyMinimalLaunchChoices()
    {
        MainWindowViewModel.PrimaryActions.Should().BeEquivalentTo([
            "Load Script",
            "Play/Pause",
            "Adjust Speed",
            "Display Settings"
        ]);
    }

    [Fact]
    public void PrimaryActions_ShouldCoverEpic4MinimalControlPanelScope()
    {
        MainWindowViewModel.PrimaryActions.Should().Contain("Load Script");
        MainWindowViewModel.PrimaryActions.Should().Contain("Play/Pause");
        MainWindowViewModel.PrimaryActions.Should().Contain("Adjust Speed");
        MainWindowViewModel.PrimaryActions.Should().Contain("Display Settings");
    }

    [Fact]
    public void ForbiddenTerms_ShouldNotAppearInPrimaryActionLabels()
    {
        var labels = string.Join(" ", MainWindowViewModel.PrimaryActions);

        foreach (var forbidden in MainWindowViewModel.ForbiddenTerms)
        {
            labels.ToLowerInvariant().Should().NotContain(forbidden.ToLowerInvariant());
        }
    }

    [Fact]
    public void TryOpenLoadedScriptInExternalEditor_ReturnsFalse_WithWarning_WhenNoScriptLoaded()
    {
        var editor = new FakeExternalEditorService();
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), editorService: editor);

        var ok = vm.TryOpenLoadedScriptInExternalEditor(out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        editor.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task TryOpenLoadedScriptInExternalEditor_CallsServiceWithSourcePathAndConfiguredEditor()
    {
        var editor = new FakeExternalEditorService();
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), editorService: editor);
        var talkScript = TestHelpers.GetAbsoluteTestPath("scripts", "talk.txt");
        var editorPath = TestHelpers.GetAbsoluteTestPath("Tools", "editor.exe");
        await vm.LoadScriptAsync(talkScript);
        vm.SetExternalEditorPath(editorPath);

        var ok = vm.TryOpenLoadedScriptInExternalEditor(out var warning);

        ok.Should().BeTrue();
        warning.Should().BeNullOrEmpty();
        editor.ReceivedScriptPath.Should().Be(talkScript);
        editor.ReceivedEditorPath.Should().Be(editorPath);
        vm.StatusMessage.Should().Contain(talkScript);
    }

    [Fact]
    public async Task TryOpenLoadedScriptInExternalEditor_PropagatesWarning_WhenServiceFails()
    {
        var editor = new FakeExternalEditorService { Result = false, WarningToReturn = "Could not launch \"bad.exe\"." };
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), editorService: editor);
        var talkScript = TestHelpers.GetAbsoluteTestPath("scripts", "talk.txt");
        await vm.LoadScriptAsync(talkScript);

        var ok = vm.TryOpenLoadedScriptInExternalEditor(out var warning);

        ok.Should().BeFalse();
        warning.Should().Be("Could not launch \"bad.exe\".");
    }

    [Fact]
    public void SetExternalEditorPath_TrimsValue_AndNormalizesNullToEmpty()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath());
        var editorPath = TestHelpers.GetAbsoluteTestPath("Tools", "editor.exe");

        vm.SetExternalEditorPath($"  {editorPath}  ");
        vm.ExternalEditorPath.Should().Be(editorPath);

        vm.SetExternalEditorPath(null);
        vm.ExternalEditorPath.Should().Be(string.Empty);

        vm.SetExternalEditorPath("   ");
        vm.ExternalEditorPath.Should().Be(string.Empty);
    }

    [Fact]
    public async Task ReloadScriptAsync_Succeeds_AndUpdatesContent_WhenFileIsReadable()
    {
        var reader = new ConfigurableReader { Content = "original content" };
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), reader);
        var talkScript = TestHelpers.GetAbsoluteTestPath("scripts", "talk.txt");
        await vm.LoadScriptAsync(talkScript);
        vm.LoadedScript.Should().NotBeNull();
        vm.LoadedScript!.Content.Should().Be("original content");

        reader.Content = "new content";

        var ok = await vm.ReloadScriptAsync();

        ok.Should().BeTrue();
        vm.LoadedScript.Content.Should().Be("new content");
        vm.ErrorMessage.Should().BeNull();
        vm.WindowBehaviorWarning.Should().BeNull();
        vm.StatusMessage.Should().Contain("Auto-reloaded");
    }

    [Fact]
    public async Task ReloadScriptAsync_Fails_AndSetsWarning_WhenFileReadFails()
    {
        var reader = new ConfigurableReader { Content = "original content" };
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), reader);
        var talkScript = TestHelpers.GetAbsoluteTestPath("scripts", "talk.txt");
        await vm.LoadScriptAsync(talkScript);
        vm.LoadedScript.Should().NotBeNull();
        vm.LoadedScript!.Content.Should().Be("original content");

        reader.ShouldFail = true;

        var ok = await vm.ReloadScriptAsync();

        ok.Should().BeFalse();
        // The script content should remain unchanged (robustness)
        vm.LoadedScript.Content.Should().Be("original content");
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.WindowBehaviorWarning.Should().NotBeNullOrEmpty();
    }

    private class ConfigurableReader : IScriptFileReader
    {
        public string Content { get; set; } = "initial";
        public bool ShouldFail { get; set; }
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        {
            if (ShouldFail) throw new System.IO.IOException("read error");
            return Task.FromResult(Content);
        }
    }
}
