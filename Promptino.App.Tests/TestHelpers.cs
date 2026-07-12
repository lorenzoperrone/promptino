using Promptino.App.Services;
using Promptino.App.ViewModels;
using Promptino.Core.Scripts;
using Promptino.Platform;
using Promptino.Storage.Settings;

namespace Promptino.App.Tests;

internal static class TestHelpers
{
    /// <summary>
    /// Returns a temp file path that does not exist on disk — no cleanup needed.
    /// </summary>
    internal static string TempPath()
        => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    internal static string GetAbsoluteTestPath(params string[] segments)
    {
        var root = Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "/";
        return Path.Combine(root, Path.Combine(segments));
    }

    internal static MainWindowViewModel CreateVm(string settingsPath, IScriptFileReader? reader = null, IExternalEditorService? editorService = null)
        => new(
            new ScriptLoaderService(reader ?? new FakeReader("hello"), new ScriptTextTransformer()),
            new AppSettingsStore(settingsPath),
            editorService);
}

/// <summary>Records open requests; configurable result/warning to simulate launch outcomes.</summary>
internal sealed class FakeExternalEditorService : IExternalEditorService
{
    public string? ReceivedScriptPath;
    public string? ReceivedEditorPath;
    public bool Result = true;
    public string WarningToReturn = string.Empty;
    public int CallCount;

    public bool TryOpenScript(string? scriptPath, string? configuredEditorPath, out string warningMessage)
    {
        CallCount++;
        ReceivedScriptPath = scriptPath;
        ReceivedEditorPath = configuredEditorPath;
        warningMessage = WarningToReturn;
        return Result;
    }
}

/// <summary>Returns a fixed string for any read request.</summary>
internal sealed class FakeReader(string content) : IScriptFileReader
{
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => Task.FromResult(content);
}

/// <summary>Always throws IOException to simulate an unreadable file.</summary>
internal sealed class FailingReader : IScriptFileReader
{
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => throw new IOException("no file");
}

/// <summary>Fails on the first call, succeeds on subsequent calls.</summary>
internal sealed class SequenceReader : IScriptFileReader
{
    private int _calls;

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        _calls++;
        if (_calls == 1) throw new IOException("first fail");
        return Task.FromResult("retry success");
    }
}
