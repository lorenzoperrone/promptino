using System;

namespace Promptino.Core;

/// <summary>
/// Provides high-performance word counting utilities without allocating temporary string arrays.
/// </summary>
internal static class WordCounter
{
    /// <summary>
    /// Counts words in the specified text by iterating through character spans.
    /// A word is defined as a contiguous sequence of non-whitespace characters.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The total number of words found in the text.</returns>
    internal static int Count(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var span = text.AsSpan();
        int count = 0;
        bool inWord = false;

        foreach (var c in span)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                count++;
                inWord = true;
            }
        }

        return count;
    }
}
