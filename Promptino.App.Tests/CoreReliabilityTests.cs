using Promptino.Core.Playback;
using Promptino.Core.Scripts;

namespace Promptino.App.Tests;

public class CoreReliabilityTests
{
    [Fact]
    public void ScriptDocument_CapturesSourceContentAndMarkdownFlag()
    {
        var document = new ScriptDocument("D:/scripts/demo.md", "Intro\nBody", true);

        document.SourcePath.Should().Be("D:/scripts/demo.md");
        document.Content.Should().Be("Intro\nBody");
        document.IsMarkdown.Should().BeTrue();
    }

    [Fact]
    public void ScriptTransformer_Markdown_CleansReadingSyntaxWithoutRenderer()
    {
        var transformer = new ScriptTextTransformer();
        var input = "# Demo\n\n- **Open** Promptino\n- Keep _eye contact_\n\n\nEnd";

        var output = transformer.Transform(input, ".md");

        output.Should().Be("Demo\nOpen Promptino\nKeep eye contact\n\nEnd");
    }

    [Theory]
    [InlineData(".md")]
    [InlineData(".MD")]
    [InlineData(".Md")]
    public void ScriptTransformer_MarkdownExtension_IsCaseInsensitive(string extension)
    {
        var transformer = new ScriptTextTransformer();

        var output = transformer.Transform("## Title", extension);

        output.Should().Be("Title");
    }

    [Theory]
    [InlineData(5, ReadingSpeed.MinWpm)]
    [InlineData(20, 20)]
    [InlineData(130, 130)]
    [InlineData(500, 500)]
    [InlineData(600, ReadingSpeed.MaxWpm)]
    public void ReadingSpeed_Clamp_StaysWithinBoundaries(int input, int expected)
    {
        ReadingSpeed.Clamp(input).Should().Be(expected);
    }

    [Fact]
    public void ReadingSpeed_WordsPerSecond_UsesClampedValue()
    {
        ReadingSpeed.WordsPerSecond(600).Should().BeApproximately(ReadingSpeed.MaxWpm / 60d, 0.0001);
        ReadingSpeed.WordsPerSecond(5).Should().BeApproximately(ReadingSpeed.MinWpm / 60d, 0.0001);
    }

    [Fact]
    public void ScriptTransformer_NonMarkdown_ReturnsUnchanged()
    {
        var transformer = new ScriptTextTransformer();
        var input = "# Keep *all* symbols";

        var output = transformer.Transform(input, ".txt");

        output.Should().Be(input);
    }

    [Fact]
    public void ScriptTransformer_RemovesSubtitleCueNumbersAndTimestamps_FromTxt()
    {
        var transformer = new ScriptTextTransformer();
        var input = "WEBVTT\n\n1\n00:02:19.451 --> 00:02:22.715\nHello there\n\n[00:02:25.000]\nGeneral Kenobi";

        var output = transformer.Transform(input, ".txt");

        output.Should().Be("Hello there\n\nGeneral Kenobi");
    }

    [Fact]
    public void ScriptTransformer_RemovesMetadataRowsAndFrontMatter()
    {
        var transformer = new ScriptTextTransformer();
        var input = "---\nTitle: Demo\nLanguage: it\n---\nSpeaker: Lorenzo\nBody line";

        var output = transformer.Transform(input, ".md");

        output.Should().Be("Body line");
    }

    [Fact]
    public void ScriptTransformer_FlattensMarkdownTablesIntoReadableRows()
    {
        var transformer = new ScriptTextTransformer();
        var input = "| Column A | Column B |\n| --- | --- |\n| Hello | World |\n| Stay | Calm |";

        var output = transformer.Transform(input, ".md");

        output.Should().Be("Column A - Column B\nHello - World\nStay - Calm");
    }

    [Fact]
    public void ScriptTransformer_Markdown_StripsLinkUrlAndKeepsLinkText()
    {
        var transformer = new ScriptTextTransformer();
        var output = transformer.Transform("Visit [our site](https://example.com) today", ".md");
        output.Should().Be("Visit our site today");
    }

    [Fact]
    public void ScriptTransformer_Markdown_RemovesImageSyntaxAndKeepsAlt()
    {
        var transformer = new ScriptTextTransformer();
        var output = transformer.Transform("Logo: ![Promptino](logo.png) here", ".md");
        output.Should().Be("Logo: Promptino here");
    }

    [Fact]
    public void ScriptTransformer_Markdown_DropsFencedCodeBlocks()
    {
        var transformer = new ScriptTextTransformer();
        var input = "Before\n\n```csharp\nvar x = 1;\n```\n\nAfter";
        var output = transformer.Transform(input, ".md");
        output.Should().NotContain("var x");
        output.Should().NotContain("```");
        output.Should().Contain("Before");
        output.Should().Contain("After");
    }

    [Fact]
    public void ScriptTransformer_Markdown_StripsBlockquoteMarker()
    {
        var transformer = new ScriptTextTransformer();
        var output = transformer.Transform("> Quoted line\nNormal line", ".md");
        output.Should().Be("Quoted line\nNormal line");
    }

    [Fact]
    public void ScriptTransformer_Markdown_StripsInlineCodeBackticks()
    {
        var transformer = new ScriptTextTransformer();
        var output = transformer.Transform("Use `dotnet build` to compile", ".md");
        output.Should().Be("Use dotnet build to compile");
    }

    [Fact]
    public void ScriptTransformer_LongMarkdown_CompletesQuickly()
    {
        // Smoke test: a 100k-word markdown script must transform within a reasonable budget.
        var input = string.Join('\n', Enumerable.Repeat("# heading **word** _emph_ [t](u) `c`", 5_000));
        var transformer = new ScriptTextTransformer();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var output = transformer.Transform(input, ".md");

        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
            "transforming a large markdown script should not block the UI");
        output.Length.Should().BeGreaterThan(0);
    }
}
