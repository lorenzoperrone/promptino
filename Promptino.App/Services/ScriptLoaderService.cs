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
    static LocalScriptFileReader()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await Promptino.Storage.IoRetry.RunAsync(async ct => await File.ReadAllBytesAsync(path, ct), cancellationToken);
        return DetectAndReadText(bytes);
    }

    public static string DetectAndReadText(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            var strictUtf8 = new System.Text.UTF8Encoding(false, true);
            return strictUtf8.GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            try
            {
                var win1252 = System.Text.Encoding.GetEncoding(1252);
                return win1252.GetString(bytes);
            }
            catch
            {
                return System.Text.Encoding.Latin1.GetString(bytes);
            }
        }
    }
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
                return ScriptLoadResult.Fail("Unsupported file type. Choose a .txt, .md, .srt, or .vtt file.");
            }

            var raw = await _fileReader.ReadAllTextAsync(path, cancellationToken);
            var transformed = _transformer.Transform(raw, extension);
            var finalCleaned = ScriptMarkerParser.ParseAndRemoveMarkers(transformed, out var markers);
            if ((markers == null || markers.Count == 0) && (string.Equals(extension, ".srt", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".vtt", StringComparison.OrdinalIgnoreCase)))
            {
                markers = ScriptMarkerParser.ParseSubtitleMarkers(raw);
            }
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
