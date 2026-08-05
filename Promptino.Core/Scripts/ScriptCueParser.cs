using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Promptino.Core.Scripts;

/// <summary>
/// Specifies the classification of a script text cue segment.
/// </summary>
public enum CueTokenType
{
    /// <summary>Standard spoken script text.</summary>
    Text,

    /// <summary>Stage direction or parenthetical instruction (e.g. (laughs), [PAUSE]).</summary>
    StageDirection,

    /// <summary>Speaker label prefix (e.g. ALICE: or [SPEAKER 1]:).</summary>
    Speaker
}

/// <summary>
/// Represents a parsed token within a script line.
/// </summary>
/// <param name="Text">The string content of the token.</param>
/// <param name="Type">The cue token type classification.</param>
public sealed record ScriptCueToken(string Text, CueTokenType Type);

/// <summary>
/// Parses script lines to extract speaker prefixes and stage direction tokens for UI styling.
/// </summary>
public sealed partial class ScriptCueParser
{
    [GeneratedRegex(@"^(\[[A-Z0-9_\s]{2,20}\]:?|[A-Z0-9_\s]{2,15}:)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SpeakerPrefixRegex();

    [GeneratedRegex(@"(\((?:[^()]+|\([^()]*\))*\)|\[(?!\[marker)[^\]]+\])")]
    private static partial Regex StageDirectionRegex();

    /// <summary>
    /// Parses a line of script text into a sequence of classified cue tokens (<see cref="ScriptCueToken"/>).
    /// </summary>
    /// <param name="line">The script line to parse.</param>
    /// <returns>A read-only list of parsed cue tokens.</returns>
    public static IReadOnlyList<ScriptCueToken> ParseTokens(string line)
    {
        var tokens = new List<ScriptCueToken>();
        if (string.IsNullOrWhiteSpace(line))
        {
            if (!string.IsNullOrEmpty(line))
                tokens.Add(new ScriptCueToken(line, CueTokenType.Text));
            return tokens;
        }

        var textToParse = line;
        var speakerMatch = SpeakerPrefixRegex().Match(textToParse);
        if (speakerMatch.Success && speakerMatch.Index == 0)
        {
            tokens.Add(new ScriptCueToken(speakerMatch.Value, CueTokenType.Speaker));
            textToParse = textToParse.Substring(speakerMatch.Length);
        }

        if (string.IsNullOrEmpty(textToParse))
            return tokens;

        var matches = StageDirectionRegex().Matches(textToParse);
        if (matches.Count == 0)
        {
            tokens.Add(new ScriptCueToken(textToParse, CueTokenType.Text));
            return tokens;
        }

        int lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                var textBetween = textToParse.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(textBetween))
                    tokens.Add(new ScriptCueToken(textBetween, CueTokenType.Text));
            }

            tokens.Add(new ScriptCueToken(match.Value, CueTokenType.StageDirection));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < textToParse.Length)
        {
            var remaining = textToParse.Substring(lastIndex);
            if (!string.IsNullOrEmpty(remaining))
                tokens.Add(new ScriptCueToken(remaining, CueTokenType.Text));
        }

        return tokens;
    }
}
