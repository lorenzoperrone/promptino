using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Promptino.Storage;

internal static class IoRetry
{
    private static readonly int[] DelaysMs = [100, 200, 400];

    internal static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
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

    internal static async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct)
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
}
