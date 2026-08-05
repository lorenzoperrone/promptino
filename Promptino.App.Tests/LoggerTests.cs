using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Promptino.Platform;
using Xunit;

namespace Promptino.App.Tests;

public class LoggerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private string GetTempLogFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PromptinoTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        _tempDirs.Add(tempDir);
        return Path.Combine(tempDir, "logs", "promptino.log");
    }

    private class FakePathProvider : IAppDataPathProvider
    {
        private readonly string _logPath;
        public FakePathProvider(string logPath) => _logPath = logPath;
        public string GetSettingsFilePath() => string.Empty;
        public string GetLogFilePath() => _logPath;
        public string GetRecentFilesFilePath() => string.Empty;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void FileLogger_ShouldCreateDirectoryAndFile_WhenLogInfoIsCalled()
    {
        var logFile = GetTempLogFile();
        var provider = new FakePathProvider(logFile);
        var logger = new FileLogger(provider);

        logger.LogInfo("Test info message");

        File.Exists(logFile).Should().BeTrue();
        var lines = File.ReadAllLines(logFile);
        lines.Should().ContainSingle();
        lines[0].Should().Contain("[INFO] Test info message");
    }

    [Fact]
    public void FileLogger_ShouldWriteCorrectFormat_ToLogFile()
    {
        var logFile = GetTempLogFile();
        var provider = new FakePathProvider(logFile);
        var logger = new FileLogger(provider);

        logger.LogWarning("Test warning message");
        logger.LogError("Test error message", new InvalidOperationException("boom"));

        var content = File.ReadAllText(logFile);
        content.Should().Contain("[WARNING] Test warning message");
        content.Should().Contain("[ERROR] Test error message - Exception: boom");
    }

    [Fact]
    public async Task FileLogger_ShouldWriteConcurrently_WithoutException()
    {
        var logFile = GetTempLogFile();
        var provider = new FakePathProvider(logFile);
        var logger = new FileLogger(provider);

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() => logger.LogInfo($"Concurrent message {index}")));
        }

        await Task.WhenAll(tasks);

        var lines = File.ReadAllLines(logFile);
        lines.Length.Should().Be(50);
    }

    [Fact]
    public void FileLogger_ShouldSwallowErrors_WhenDirectoryIsUnwritable()
    {
        var provider = new FakePathProvider("::illegal::path::chars");
        var logger = new FileLogger(provider);

        Action act = () => logger.LogInfo("This should swallow exception");
        act.Should().NotThrow();
    }

    [Fact]
    public void WindowsAppDataPathProvider_ShouldResolvePromptinoLogPath()
    {
        var provider = new WindowsAppDataPathProvider();
        var path = provider.GetLogFilePath();
        path.Should().EndWith(Path.Combine("Promptino", "logs", "promptino.log"));
        path.Should().Contain(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    [Fact]
    public void FileLogger_ShouldRotateFiles_WhenMaxLogFileSizeIsExceeded()
    {
        var logFile = GetTempLogFile();
        var provider = new FakePathProvider(logFile);
        var logger = new FileLogger(provider)
        {
            MaxLogFileSize = 100,
            MaxBackupFiles = 2
        };

        // Write a message that exceeds 100 bytes
        logger.LogInfo(new string('B', 120));
        File.Exists(logFile).Should().BeTrue();
        File.Exists($"{logFile}.1").Should().BeFalse();

        // Write second message -> triggers rotation since file is > 100 bytes
        logger.LogInfo("Second message");

        File.Exists(logFile).Should().BeTrue();
        File.Exists($"{logFile}.1").Should().BeTrue();

        var rolledContent = File.ReadAllText($"{logFile}.1");
        rolledContent.Should().Contain(new string('B', 120));

        var newContent = File.ReadAllText(logFile);
        newContent.Should().Contain("Second message");
        newContent.Should().NotContain(new string('B', 120));
    }

    [Fact]
    public void FileLogger_ShouldSanitizeUserProfileAndAppDataPaths()
    {
        var logFile = GetTempLogFile();
        var provider = new FakePathProvider(logFile);
        var logger = new FileLogger(provider);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var message = $"User home is at {userProfile} and app data is at {appData}.";
        logger.LogInfo(message);

        var logContent = File.ReadAllText(logFile);
        logContent.Should().NotContain(userProfile);
        logContent.Should().NotContain(appData);
        logContent.Should().Contain("%USERPROFILE%");
        logContent.Should().Contain("%APPDATA%");
    }
}
