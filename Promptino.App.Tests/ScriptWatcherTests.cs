using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Promptino.Platform;
using Xunit;

namespace Promptino.App.Tests;

public class ScriptWatcherTests : IDisposable
{
    private readonly string _tempFile;

    public ScriptWatcherTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"script_watcher_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_tempFile, "initial content");
    }

    [Fact]
    public async Task Watcher_ShouldFireCallback_WhenFileIsModified()
    {
        using var watcher = new ScriptWatcher();
        var tcs = new TaskCompletionSource<bool>();

        watcher.StartWatching(_tempFile, () => tcs.TrySetResult(true));

        // Wait a small buffer for OS filesystem registration
        await Task.Delay(100);

        // Modify the file
        await File.WriteAllTextAsync(_tempFile, "modified content");

        // Wait for the event with a timeout
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        completedTask.Should().Be(tcs.Task, "because the file watcher should detect the file modification");
    }

    [Fact]
    public async Task Watcher_ShouldDebounceMultipleRapidModifications()
    {
        using var watcher = new ScriptWatcher();
        int callCount = 0;

        watcher.StartWatching(_tempFile, () =>
        {
            Interlocked.Increment(ref callCount);
        });

        // Wait a small buffer for OS filesystem registration
        await Task.Delay(100);

        // Modify the file twice rapidly
        await File.WriteAllTextAsync(_tempFile, "modification 1");
        await File.WriteAllTextAsync(_tempFile, "modification 2");

        // Wait a short time to see if more than 1 call occurs
        await Task.Delay(1000);

        callCount.Should().Be(1, "because rapid consecutive changes within 500ms should be debounced");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // Ignore best-effort cleanup exceptions
        }
    }
}
