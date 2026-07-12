using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Promptino.Core.Scripts;

public sealed partial class ScriptTextTransformer
{
    public sealed record ScriptCleanupOptions(
        bool RemoveTimestamps = true,
        bool RemoveMetadataRows = true,
        bool RemoveMarkdownTables = true,
        bool ApplyMarkdownCleanup = true,
        bool CollapseBlankLines = true);

    // Block-level constructs (must run before inline cleanup).

    [GeneratedRegex(@"^\s*>\s?", RegexOptions.Multiline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^#{1,6}\s*", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    // Inline constructs. Image must run BEFORE link because image syntax (![alt](url))
    // is a superset of link syntax ([text](url)) and would otherwise be misparsed.
    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]+\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(.*?)\*\*")]
    private static partial Regex BoldAsteriskRegex();

    [GeneratedRegex(@"__(.*?)__")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicAsteriskRegex();

    [GeneratedRegex(@"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveBlankLinesRegex();
    [GeneratedRegex(@"^(?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?\s*--?>\s*(?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?$", RegexOptions.IgnoreCase)]
    private static partial Regex TimestampRangeRegex();
    [GeneratedRegex(@"^(?:\[(?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?\]|(?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?)$", RegexOptions.IgnoreCase)]
    private static partial Regex TimestampMarkerRegex();
    [GeneratedRegex(@"^(?:Language|Source|Author|Title|Date|Tags|Speaker|Duration|Version|Locale|Notes|Topic|Chapter|Section)\s*:\s+.+$", RegexOptions.IgnoreCase)]
    private static partial Regex MetadataRowRegex();
    [GeneratedRegex(@"^\|?(?:\s*:?-{3,}:?\s*\|)+\s*:?-{3,}:?\s*\|?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownTableSeparatorRegex();
    [GeneratedRegex(@"^\|.*\|$")]
    private static partial Regex MarkdownTableRowRegex();

    public string Transform(string rawContent, string extension)
        => Transform(rawContent, extension, new ScriptCleanupOptions());

    public string Transform(string rawContent, string extension, ScriptCleanupOptions options)
    {
        var text = rawContent.Replace("\r\n", "\n");
        var isMarkdown = string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);

        text = ApplyLineFilters(text, options);
        if (!isMarkdown || !options.ApplyMarkdownCleanup)
            return options.CollapseBlankLines ? ExcessiveBlankLinesRegex().Replace(text, "\n\n").Trim() : text.Trim();

        // Block-level: drop fenced code blocks entirely (not read aloud), strip blockquote markers.
        text = BlockquoteRegex().Replace(text, string.Empty);
        text = HeadingRegex().Replace(text, string.Empty);
        text = BulletRegex().Replace(text, string.Empty);

        // Inline: keep readable text, drop URL noise; image alt text replaces the image, link text replaces the link.
        text = ImageRegex().Replace(text, "$1");
        text = LinkRegex().Replace(text, "$1");
        text = InlineCodeRegex().Replace(text, "$1");

        text = BoldAsteriskRegex().Replace(text, "$1");
        text = BoldUnderscoreRegex().Replace(text, "$1");
        text = ItalicAsteriskRegex().Replace(text, "$1");
        text = ItalicUnderscoreRegex().Replace(text, "$1");
        if (options.CollapseBlankLines)
            text = ExcessiveBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    private static string ApplyLineFilters(string input, ScriptCleanupOptions options)
    {
        var lines = input.Split('\n');
        var output = new List<string>(lines.Length);
        var skipFrontMatter = false;
        var inFence = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            var trimmedStart = line.TrimStart();
            var nextTrimmed = i + 1 < lines.Length ? lines[i + 1].Trim() : string.Empty;

            // Fenced code blocks: skip entirely
            if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence) continue;

            if (options.RemoveMetadataRows && i == 0 && trimmed == "---")
            {
                skipFrontMatter = true;
                continue;
            }

            if (skipFrontMatter)
            {
                if (trimmed is "---" or "...")
                    skipFrontMatter = false;
                continue;
            }

            if (options.RemoveTimestamps)
            {
                if (trimmed.Equals("WEBVTT", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsSubtitleCueIndex(trimmed, nextTrimmed) || IsTimestampNoise(trimmed))
                    continue;
            }

            if (options.RemoveMetadataRows && MetadataRowRegex().IsMatch(trimmed))
                continue;

            if (options.RemoveMarkdownTables)
            {
                if (MarkdownTableSeparatorRegex().IsMatch(trimmed))
                    continue;

                if (MarkdownTableRowRegex().IsMatch(trimmed))
                {
                    output.Add(FlattenMarkdownTableRow(trimmed));
                    continue;
                }
            }

            output.Add(line);
        }

        return string.Join('\n', output);
    }

    private static bool IsSubtitleCueIndex(string trimmed, string nextTrimmed)
        => int.TryParse(trimmed, out _)
           && !string.IsNullOrWhiteSpace(nextTrimmed)
           && TimestampRangeRegex().IsMatch(nextTrimmed);

    private static bool IsTimestampNoise(string trimmed)
        => TimestampRangeRegex().IsMatch(trimmed) || TimestampMarkerRegex().IsMatch(trimmed);

    private static string FlattenMarkdownTableRow(string trimmed)
    {
        var cells = trimmed.Trim('|')
            .Split('|', StringSplitOptions.TrimEntries)
            .Where(cell => !string.IsNullOrWhiteSpace(cell))
            .ToArray();

        return cells.Length == 0 ? string.Empty : string.Join(" - ", cells);
    }
}
