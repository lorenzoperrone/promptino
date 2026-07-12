using System;

namespace Promptino.Core;

internal static class WordCounter
{
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
