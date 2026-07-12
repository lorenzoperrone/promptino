using Promptino.Core.Playback;
using System.Collections.Generic;

namespace Promptino.Core.Scripts;

public sealed record ScriptDocument(string SourcePath, string Content, bool IsMarkdown, string? RawContent = null, IReadOnlyList<ScriptMarker>? Markers = null);
