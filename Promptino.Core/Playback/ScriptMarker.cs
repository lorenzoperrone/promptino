namespace Promptino.Core.Playback;

/// <summary>
/// Represents a navigation marker embedded in teleprompter script text.
/// </summary>
/// <param name="Id">Unique identifier for the marker.</param>
/// <param name="Label">Human-readable marker title displayed in the marker list.</param>
/// <param name="Order">0-based sequential position index in the script.</param>
/// <param name="ProgressRatio">Normalized scroll position ratio (0.0 to 1.0) of the marker.</param>
public record ScriptMarker(string Id, string Label, int Order, double ProgressRatio);
