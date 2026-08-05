using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Promptino.Storage.Settings;

/// <summary>
/// Represents an entry in the recently opened scripts history.
/// </summary>
/// <param name="Path">Full file path of the script.</param>
/// <param name="LastOpenedUtc">UTC timestamp when the file was last opened.</param>
public sealed record RecentFileEntry(string Path, DateTime LastOpenedUtc);

/// <summary>
/// Manages persistence and history tracking for recently opened script files.
/// </summary>
public sealed class RecentFilesStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>Gets or sets the maximum number of recent file entries to retain (default 10).</summary>
    public int MaxEntries { get; set; } = 10;

    /// <summary>
    /// Initializes a new instance of <see cref="RecentFilesStore"/> with the target file path.
    /// </summary>
    public RecentFilesStore(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Loads recent file history from JSON storage asynchronously.
    /// </summary>
    public async Task<(IReadOnlyList<RecentFileEntry> Entries, bool Recovered)> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return (Array.Empty<RecentFileEntry>(), false);
        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            var items = JsonSerializer.Deserialize(json, JsonStorageOptions.Context.ListRecentFileEntry);
            return (items ?? new List<RecentFileEntry>(), false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecentFilesStore] LoadAsync error: {ex.Message}");
            return (Array.Empty<RecentFileEntry>(), true);
        }
    }

    /// <summary>
    /// Saves the recent file entries collection asynchronously.
    /// </summary>
    public async Task<bool> SaveAllAsync(IReadOnlyList<RecentFileEntry> entries, CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try { return await SaveAllCoreAsync(entries, ct); }
        finally { _saveLock.Release(); }
    }

    private async Task<bool> SaveAllCoreAsync(IReadOnlyList<RecentFileEntry> entries, CancellationToken ct)
    {
        var tempPath = _path + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(entries.ToList(), JsonStorageOptions.Context.ListRecentFileEntry);
            await IoRetry.RunAsync(async ct2 =>
            {
                await IoRetry.WriteTextWriteThroughAsync(tempPath, json, ct2);
                File.Move(tempPath, _path, overwrite: true);
            }, ct);
            return true;
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a file path in the recent history list.
    /// </summary>
    public async Task AddFileAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        string fullPath;
        try { fullPath = Path.GetFullPath(filePath); }
        catch { return; }

        await _saveLock.WaitAsync(ct);
        try
        {
            var (entries, _) = await LoadAsync(ct);
            var list = entries.ToList();

            list.RemoveAll(e => e.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, new RecentFileEntry(fullPath, DateTime.UtcNow));

            if (list.Count > MaxEntries) list = list.Take(MaxEntries).ToList();

            await SaveAllCoreAsync(list, ct);
        }
        finally { _saveLock.Release(); }
    }

    /// <inheritdoc />
    public void Dispose() => _saveLock.Dispose();
}
