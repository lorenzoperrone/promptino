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

    private static int CountWords(string text)
    {
        return WordCounter.Count(text);
    }
}
