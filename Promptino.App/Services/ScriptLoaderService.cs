using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Promptino.Core.Scripts;

namespace Promptino.App.Services;

public interface IScriptFileReader
{
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
}

public sealed class LocalScriptFileReader : IScriptFileReader
{
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(path, cancellationToken);
}

public sealed class ScriptLoaderService
{
    private static readonly string[] SupportedExtensions = [".txt", ".md", ".srt", ".vtt"];

    private readonly IScriptFileReader _fileReader;
    private readonly ScriptTextTransformer _transformer;

    public ScriptLoaderService(IScriptFileReader fileReader, ScriptTextTransformer transformer)
    {
        _fileReader = fileReader;
        _transformer = transformer;
    }

    public async Task<ScriptLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return ScriptLoadResult.Fail("Unsupported file type. Supported formats: .txt, .md, .srt, .vtt.");
            }

            var raw = await _fileReader.ReadAllTextAsync(path, cancellationToken);
            var transformed = _transformer.Transform(raw, extension);
            var finalCleaned = ScriptMarkerParser.ParseAndRemoveMarkers(transformed, out var markers);
            var document = new ScriptDocument(path, finalCleaned, string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase), raw, markers);
            return ScriptLoadResult.Ok(document);
        }
        catch (ArgumentException)
        {
            return ScriptLoadResult.Fail("The file path contains invalid characters.");
        }
        catch (OperationCanceledException)
        {
            return ScriptLoadResult.Fail("File loading was cancelled.");
        }
        catch (FileNotFoundException)
        {
            return ScriptLoadResult.Fail("File not found. It may have been moved or deleted.");
        }
        catch (UnauthorizedAccessException)
        {
            return ScriptLoadResult.Fail("Cannot read this file. Check file permissions.");
        }
        catch (PathTooLongException)
        {
            return ScriptLoadResult.Fail("File path is too long.");
        }
        catch (IOException)
        {
            return ScriptLoadResult.Fail("Could not read this file. It may be in use or locked by another application.");
        }
    }
}
