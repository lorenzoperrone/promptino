namespace Promptino.Core.Scripts;

/// <summary>
/// Represents the result of a script loading or parsing operation.
/// </summary>
/// <param name="Success"><c>true</c> if the script loaded successfully; otherwise, <c>false</c>.</param>
/// <param name="Document">The loaded <see cref="ScriptDocument"/> when successful, or null on failure.</param>
/// <param name="ErrorMessage">The error description when failed, or null on success.</param>
public sealed record ScriptLoadResult(bool Success, ScriptDocument? Document, string? ErrorMessage)
{
    /// <summary>Creates a successful script load result.</summary>
    public static ScriptLoadResult Ok(ScriptDocument document) => new(true, document, null);

    /// <summary>Creates a failed script load result with the specified error message.</summary>
    public static ScriptLoadResult Fail(string message) => new(false, null, message);
}
