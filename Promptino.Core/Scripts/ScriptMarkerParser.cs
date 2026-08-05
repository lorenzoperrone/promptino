using Promptino.Core.Playback;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Promptino.Core.Scripts;

public sealed partial class ScriptMarkerParser
{
    [GeneratedRegex(@"\[\[marker:?([^\]]*)\]\]", RegexOptions.IgnoreCase)]
    private static partial Regex MarkerRegex();

    public static string ParseAndRemoveMarkers(string text, out IReadOnlyList<ScriptMarker> markers)
    {
        var markerList = new List<ScriptMarker>();
        if (string.IsNullOrWhiteSpace(text))
        {
            markers = markerList;
            return text ?? string.Empty;
        }

        var matches = MarkerRegex().Matches(text);
        if (matches.Count == 0)
        {
            markers = markerList;
            return text;
        }

        int currentWordCount = 0;
        int lastIndex = 0;
        int order = 1;

        var tempMarkers = new List<(string Label, int WordIndex)>();

        foreach (Match match in matches)
        {
            var textBetween = text.Substring(lastIndex, match.Index - lastIndex);
            currentWordCount += CountWords(textBetween);
            
            var label = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(label)) label = $"Marker {order}";

            tempMarkers.Add((label, currentWordCount));
            
            order++;
            lastIndex = match.Index + match.Length;
        }

        var remainingText = text.Substring(lastIndex);
        var totalWords = currentWordCount + CountWords(remainingText);

        foreach (var m in tempMarkers)
        {
            var ratio = totalWords == 0 ? 0 : (double)m.WordIndex / totalWords;
            markerList.Add(new ScriptMarker($"m_{Guid.NewGuid():N}", m.Label, markerList.Count + 1, ratio));
        }

        markers = markerList;
        return MarkerRegex().Replace(text, "");
    }

    [GeneratedRegex(@"^((?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?)\s*--?>\s*(?:\d{1,2}:)?\d{2}:\d{2}(?:[.,]\d{1,3})?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SubtitleTimestampRegex();

    public static IReadOnlyList<ScriptMarker> ParseSubtitleMarkers(string rawContent)
    {
        var markers = new List<ScriptMarker>();
        if (string.IsNullOrWhiteSpace(rawContent))
            return markers;

        rawContent = rawContent.Replace("\r\n", "\n");

        var matches = SubtitleTimestampRegex().Matches(rawContent);
        if (matches.Count == 0)
            return markers;

        var transformer = new ScriptTextTransformer();
        var tempMarkers = new List<(string Label, int WordIndex)>();

        int cumulativeWords = 0;
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            var textBefore = rawContent.Substring(lastIndex, match.Index - lastIndex);
            var cleanedBefore = transformer.Transform(textBefore, ".srt");
            cumulativeWords += WordCounter.Count(cleanedBefore);

            var timestampStr = match.Groups[1].Value.Trim();
            // Format timestamp nicely (e.g. 00:01:20,000 -> 00:01:20)
            int dotIdx = timestampStr.IndexOfAny(new[] { ',', '.' });
            if (dotIdx > 0)
            {
                timestampStr = timestampStr.Substring(0, dotIdx);
            }

            tempMarkers.Add((timestampStr, cumulativeWords));
            lastIndex = match.Index + match.Length;
        }

        var remainingText = rawContent.Substring(lastIndex);
        var cleanedRemaining = transformer.Transform(remainingText, ".srt");
        var totalWords = cumulativeWords + WordCounter.Count(cleanedRemaining);

        int order = 1;
        foreach (var m in tempMarkers)
        {
            var ratio = totalWords == 0 ? 0 : (double)m.WordIndex / totalWords;
            markers.Add(new ScriptMarker($"m_{Guid.NewGuid():N}", m.Label, order++, ratio));
        }

        return markers;
    }

    private static int CountWords(string text)
    {
        return WordCounter.Count(text);
    }
}
