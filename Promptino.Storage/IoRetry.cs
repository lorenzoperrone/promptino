using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Promptino.Storage;

/// <summary>
/// Provides resilient I/O retry wrappers and atomic write-through stream operations.
/// </summary>
public static class IoRetry
{
    private static readonly int[] DelaysMs = [100, 200, 400];

    /// <summary>
    /// Executes an asynchronous function returning a result, retrying up to 3 times on transient <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    /// <typeparam name="T">The return value type.</typeparam>
    /// <param name="action">The asynchronous operation delegate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result returned by <paramref name="action"/>.</returns>
    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var lastEx = default(Exception?);
        for (int i = 0; i <= DelaysMs.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i > 0) await Task.Delay(DelaysMs[i - 1], ct);

            try { return await action(ct); }
            catch (IOException ex) when (i < DelaysMs.Length) { lastEx = ex; }
            catch (UnauthorizedAccessException ex) when (i < DelaysMs.Length) { lastEx = ex; }
        }
        throw lastEx ?? new IOException("IO operation failed after retries.");
    }

    /// <summary>
    /// Executes an asynchronous action, retrying up to 3 times on transient <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    /// <param name="action">The asynchronous operation delegate.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var lastEx = default(Exception?);
        for (int i = 0; i <= DelaysMs.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i > 0) await Task.Delay(DelaysMs[i - 1], ct);

            try { await action(ct); return; }
            catch (IOException ex) when (i < DelaysMs.Length) { lastEx = ex; }
            catch (UnauthorizedAccessException ex) when (i < DelaysMs.Length) { lastEx = ex; }
        }
        throw lastEx ?? new IOException("IO operation failed after retries.");
    }

    /// <summary>
    /// Writes text to a file using OS <see cref="FileOptions.WriteThrough"/> and explicit stream flushing to guarantee physical persistence.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="text">Text content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteTextWriteThroughAsync(string path, string text, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
            PreallocationSize = bytes.Length
        };
        await using var stream = new FileStream(path, fileOptions);
        await stream.WriteAsync(bytes, ct);
        stream.Flush(true);
    }
}
