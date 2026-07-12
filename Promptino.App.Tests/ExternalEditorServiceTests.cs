using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using Promptino.Platform;
using FluentAssertions;
using Xunit;

namespace Promptino.App.Tests;

public class ExternalEditorServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    /// <summary>Records launch requests; optionally throws to simulate an OS launch failure.</summary>
    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public string? LaunchedFileName;
        public List<string> LaunchedArguments = new();
        public string? LaunchedWorkingDirectory;
        public int LaunchCount;
        public Exception? ThrowOnLaunch;

        public void Launch(string fileName, IEnumerable<string> arguments, string? workingDirectory = null)
        {
            LaunchCount++;
            if (ThrowOnLaunch is not null) throw ThrowOnLaunch;
            LaunchedFileName = fileName;
            LaunchedArguments = new List<string>(arguments);
            LaunchedWorkingDirectory = workingDirectory;
        }
    }

    private string CreateTempScript()
    {
        var path = TestHelpers.TempPath();
        File.WriteAllText(path, "script content");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void TryOpenScript_LaunchesNotepad_WhenNoEditorConfigured()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();

        var ok = service.TryOpenScript(script, null, out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        launcher.LaunchedFileName.Should().Be(ExternalEditorService.DefaultEditorFileName);
        launcher.LaunchedArguments.Should().ContainSingle().Which.Should().Be(script);
    }

    [Fact]
    public void TryOpenScript_LaunchesNotepad_WhenConfiguredEditorIsWhitespace()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();

        var ok = service.TryOpenScript(script, "   ", out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        launcher.LaunchedFileName.Should().Be(ExternalEditorService.DefaultEditorFileName);
    }

    [Fact]
    public void TryOpenScript_LaunchesConfiguredEditor_WithScriptPathAsSingleArgument()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var editorPath = TestHelpers.GetAbsoluteTestPath("Tools", "My Editor", "editor.exe");

        var ok = service.TryOpenScript(script, editorPath, out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        launcher.LaunchedFileName.Should().Be(editorPath);
        launcher.LaunchedArguments.Should().ContainSingle().Which.Should().Be(script);
        launcher.LaunchCount.Should().Be(1);
    }

    [Fact]
    public void TryOpenScript_TrimsConfiguredEditorPath()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var editorPath = TestHelpers.GetAbsoluteTestPath("Tools", "editor.exe");

        var ok = service.TryOpenScript(script, $"  {editorPath}  ", out _);

        ok.Should().BeTrue();
        launcher.LaunchedFileName.Should().Be(editorPath);
    }

    [Fact]
    public void TryOpenScript_Fails_WhenScriptPathIsNull()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);

        var ok = service.TryOpenScript(null, null, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        warning!.ToLowerInvariant().Should().Contain("script");
        launcher.LaunchCount.Should().Be(0);
    }

    [Fact]
    public void TryOpenScript_Fails_WhenScriptFileIsMissingOnDisk()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);

        var ok = service.TryOpenScript(TestHelpers.TempPath(), null, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        launcher.LaunchCount.Should().Be(0);
    }

    [Fact]
    public void TryOpenScript_Fails_WithWarningNamingEditor_WhenLaunchThrows()
    {
        var missingEditor = TestHelpers.GetAbsoluteTestPath("Missing", "editor.exe");
        var launcher = new FakeProcessLauncher { ThrowOnLaunch = new Win32Exception(2) };
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();

        var ok = service.TryOpenScript(script, missingEditor, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        warning.Should().Contain(missingEditor);
    }

    [Fact]
    public void TryOpenScript_TrimsEnclosingDoubleQuotes_FromConfiguredEditorPath()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var editorPath = TestHelpers.GetAbsoluteTestPath("Tools", "My Editor", "editor.exe");

        var ok = service.TryOpenScript(script, $"\"{editorPath}\"", out var warning);

        ok.Should().BeTrue();
        warning.Should().BeEmpty();
        launcher.LaunchedFileName.Should().Be(editorPath);
    }

    [Fact]
    public void TryOpenScript_Fails_WhenConfiguredEditorPathIsADirectory()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var tempDir = Path.GetTempPath();

        var ok = service.TryOpenScript(script, tempDir, out var warning);

        ok.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
        warning.Should().Contain("directory");
        launcher.LaunchCount.Should().Be(0);
    }

    [Fact]
    public void TryOpenScript_SetsWorkingDirectory_ToScriptDirectory()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var expectedDir = Path.GetDirectoryName(script);

        var ok = service.TryOpenScript(script, null, out _);

        ok.Should().BeTrue();
        launcher.LaunchedWorkingDirectory.Should().Be(expectedDir);
    }

    [Fact]
    public void TryOpenScript_ParsesCommandLineWithArguments_Correctly()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();

        var ok = service.TryOpenScript(script, "code --wait --new-window", out _);

        ok.Should().BeTrue();
        launcher.LaunchedFileName.Should().Be("code");
        launcher.LaunchedArguments.Should().BeEquivalentTo(new[] { "--wait", "--new-window", script });
    }

    [Fact]
    public void TryOpenScript_ParsesDoubleQuotedCommandLineWithArguments_Correctly()
    {
        var launcher = new FakeProcessLauncher();
        var service = new ExternalEditorService(launcher);
        var script = CreateTempScript();
        var editorPath = TestHelpers.GetAbsoluteTestPath("Program Files", "Editor", "edit.exe");

        var ok = service.TryOpenScript(script, $"\"{editorPath}\" --wait", out _);

        ok.Should().BeTrue();
        launcher.LaunchedFileName.Should().Be(editorPath);
        launcher.LaunchedArguments.Should().BeEquivalentTo(new[] { "--wait", script });
    }
}

public class SystemProcessLauncherTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void Launch_ResolvesAndExecutesCmdScriptSuccessfully()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            return;

        var launcher = new SystemProcessLauncher();
        var scriptPath = TestHelpers.TempPath() + ".cmd";
        var outputMarkerPath = TestHelpers.TempPath();
        
        _tempFiles.Add(scriptPath);
        _tempFiles.Add(outputMarkerPath);

        File.WriteAllText(scriptPath, $"@echo off\necho done > \"{outputMarkerPath}\"\n");

        var directory = Path.GetDirectoryName(scriptPath);
        var fileName = Path.GetFileName(scriptPath);

        var act = () => launcher.Launch(fileName, new string[0], directory);
        act.Should().NotThrow();

        int retries = 200;
        while (retries > 0 && !File.Exists(outputMarkerPath))
        {
            System.Threading.Thread.Sleep(10);
            retries--;
        }

        File.Exists(outputMarkerPath).Should().BeTrue();
    }
}
