using Promptino.Core.Playback;
using System.Collections.Generic;

namespace Promptino.Core.Scripts;

/// <summary>
/// Represents a loaded teleprompter script document and its metadata.
/// </summary>
/// <param name="SourcePath">The local file path or source identifier of the script.</param>
/// <param name="Content">The cleaned and processed script text ready for prompter rendering.</param>
/// <param name="IsMarkdown">Indicates whether the source document was a Markdown file.</param>
/// <param name="RawContent">The original uncleaned script text before transforms, if preserved.</param>
/// <param name="Markers">Navigation markers extracted from the script text.</param>
public sealed record ScriptDocument(string SourcePath, string Content, bool IsMarkdown, string? RawContent = null, IReadOnlyList<ScriptMarker>? Markers = null);
