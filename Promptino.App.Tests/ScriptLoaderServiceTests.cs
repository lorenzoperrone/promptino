using Promptino.App.Services;
using Promptino.App.ViewModels;
using Promptino.Core.Scripts;
using Promptino.Storage.Settings;

namespace Promptino.App.Tests;

public class ScriptLoaderServiceTests
{
    [Fact]
    public async Task LoadAsync_Txt_ReturnsReadableDocument()
    {
        var service = new ScriptLoaderService(new FakeReader("Hello speaker"), new ScriptTextTransformer());

        var result = await service.LoadAsync("script.txt");

        result.Success.Should().BeTrue();
        result.Document!.Content.Should().Be("Hello speaker");
        result.Document.IsMarkdown.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_Markdown_AppliesLightCleanup()
    {
        var markdown = "# Title\n- **Bold point**\n- _calm_";
        var service = new ScriptLoaderService(new FakeReader(markdown), new ScriptTextTransformer());

        var result = await service.LoadAsync("notes.md");

        result.Success.Should().BeTrue();
        result.Document!.Content.Should().Be("Title\nBold point\ncalm");
    }

    [Fact]
    public async Task LoadAsync_ReadFailure_ReturnsLocalError()
    {
        var service = new ScriptLoaderService(new FailingReader(), new ScriptTextTransformer());

        var result = await service.LoadAsync("missing.txt");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ViewModel_AfterFailure_CanRetrySuccessfully()
    {
        var vm = TestHelpers.CreateVm(TestHelpers.TempPath(), new SequenceReader());

        await vm.LoadScriptAsync("missing.txt");
        vm.ErrorMessage.Should().NotBeNull();

        await vm.LoadScriptAsync("script.txt");

        vm.ErrorMessage.Should().BeNull();
        vm.LoadedScript.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_Markdown_WithUnclosedFencedBlock_DoesNotFail()
    {
        var markdown = "# Title\n```csharp\npublic class X {\n- list\n";
        var service = new ScriptLoaderService(new FakeReader(markdown), new ScriptTextTransformer());

        var result = await service.LoadAsync("notes.md");

        result.Success.Should().BeTrue();
        result.Document!.Content.Should().Contain("Title");
    }
}
