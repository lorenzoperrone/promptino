using Promptino.Core.Scripts;

namespace Promptino.App.Tests;

public class CleanupPreviewTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void CreateCleanupOptions_MapsToggleStateToCleanupOptions(
        bool removeTimestamps,
        bool removeMetadataRows,
        bool removeMarkdownTables)
    {
        var options = MainWindow.CreateCleanupOptions(
            removeTimestamps,
            removeMetadataRows,
            removeMarkdownTables);

        options.RemoveTimestamps.Should().Be(removeTimestamps);
        options.RemoveMetadataRows.Should().Be(removeMetadataRows);
        options.RemoveMarkdownTables.Should().Be(removeMarkdownTables);
        options.ApplyMarkdownCleanup.Should().BeTrue();
        options.CollapseBlankLines.Should().BeTrue();
    }

    [Fact]
    public void CleanupOptions_CanDisableTimestampFilteringWithoutDisablingOtherCleanup()
    {
        var transformer = new ScriptTextTransformer();
        var input = "1\n00:02:19.451 --> 00:02:22.715\nHello\n\nLanguage: it\n\n| A | B |\n| --- | --- |\n| C | D |";
        var options = MainWindow.CreateCleanupOptions(
            removeTimestamps: false,
            removeMetadataRows: true,
            removeMarkdownTables: true);

        var output = transformer.Transform(input, ".md", options);

        output.Should().Contain("00:02:19.451 --> 00:02:22.715");
        output.Should().NotContain("Language: it");
        output.Should().Contain("A - B");
        output.Should().Contain("C - D");
    }
}
