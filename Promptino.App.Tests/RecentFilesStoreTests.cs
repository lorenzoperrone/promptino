using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Promptino.Storage.Settings;
using Xunit;

namespace Promptino.App.Tests;

public class RecentFilesStoreTests : IDisposable
{
    private readonly string _tempFile;

    public RecentFilesStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "RecentFilesTests_" + Path.GetRandomFileName() + ".json");
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
        catch { }
    }

    [Fact]
    public async Task RecentFilesStore_ShouldAddFiles_AndMaintainOrder()
    {
        var store = new RecentFilesStore(_tempFile);
        var file1 = Path.Combine(Path.GetTempPath(), "test1.txt");
        var file2 = Path.Combine(Path.GetTempPath(), "test2.txt");

        await store.AddFileAsync(file1);
        await store.AddFileAsync(file2);

        var (entries, recovered) = await store.LoadAsync();
        recovered.Should().BeFalse();
        entries.Count.Should().Be(2);

        entries[0].Path.Should().Be(Path.GetFullPath(file2));
        entries[1].Path.Should().Be(Path.GetFullPath(file1));
    }

    [Fact]
    public async Task RecentFilesStore_ShouldEnforceLimit()
    {
        var store = new RecentFilesStore(_tempFile) { MaxEntries = 3 };

        await store.AddFileAsync("file1.txt");
        await store.AddFileAsync("file2.txt");
        await store.AddFileAsync("file3.txt");
        await store.AddFileAsync("file4.txt");

        var (entries, _) = await store.LoadAsync();
        entries.Count.Should().Be(3);
        entries[0].Path.Should().Be(Path.GetFullPath("file4.txt"));
        entries[1].Path.Should().Be(Path.GetFullPath("file3.txt"));
        entries[2].Path.Should().Be(Path.GetFullPath("file2.txt"));
    }

    [Fact]
    public async Task RecentFilesStore_ShouldDeduplicateCaseInsensitive()
    {
        var store = new RecentFilesStore(_tempFile);

        await store.AddFileAsync("file1.txt");
        await store.AddFileAsync("file2.txt");
        await store.AddFileAsync("FILE1.TXT");

        var (entries, _) = await store.LoadAsync();
        entries.Count.Should().Be(2);
        entries[0].Path.Should().Be(Path.GetFullPath("FILE1.TXT"));
        entries[1].Path.Should().Be(Path.GetFullPath("file2.txt"));
    }

    [Fact]
    public async Task RecentFilesStore_ShouldRecoverOnCorruption()
    {
        await File.WriteAllTextAsync(_tempFile, "{ corrupt json }");
        var store = new RecentFilesStore(_tempFile);

        var (entries, recovered) = await store.LoadAsync();
        recovered.Should().BeTrue();
        entries.Should().BeEmpty();
    }
}
