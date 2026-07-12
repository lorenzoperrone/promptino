namespace Promptino.Core.Scripts;

public sealed record ScriptLoadResult(bool Success, ScriptDocument? Document, string? ErrorMessage)
{
    public static ScriptLoadResult Ok(ScriptDocument document) => new(true, document, null);

    public static ScriptLoadResult Fail(string message) => new(false, null, message);
}
