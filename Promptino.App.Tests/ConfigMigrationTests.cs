using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Promptino.Storage.Settings;
using Xunit;

namespace Promptino.App.Tests;

public class ConfigMigrationTests : IDisposable
{
    private readonly string _tempFile;

    public ConfigMigrationTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "ConfigMigrationTests_" + Path.GetRandomFileName() + ".json");
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
    public async Task AppSettingsStore_ShouldMigrateV0ToV1_AndSaveToDisk()
    {
        var preV1SettingsJson = @"
        {
            ""CalibrationCompleted"": true,
            ""DefaultWpm"": 140,
            ""Preferences"": {
                ""TextSize"": 36,
                ""LineSpacing"": 1.5,
                ""WindowOpacity"": 0.9,
                ""AlwaysOnTop"": true,
                ""FontFamily"": ""Segoe UI"",
                ""ReadingMargin"": 50,
                ""HorizontalMirror"": false
            }
        }";
        await File.WriteAllTextAsync(_tempFile, preV1SettingsJson);

        var store = new AppSettingsStore(_tempFile);

        var loadResult = await store.LoadAsync();
        loadResult.Recovered.Should().BeFalse();
        loadResult.Settings.SchemaVersion.Should().Be(1);
        loadResult.Settings.DefaultWpm.Should().Be(140);
        loadResult.Settings.Preferences.TextSize.Should().Be(36);

        var updatedJson = await File.ReadAllTextAsync(_tempFile);
        var parsedNode = JsonSerializer.Deserialize<JsonElement>(updatedJson);
        parsedNode.GetProperty("SchemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ProfileStore_ShouldMigrateV0ToV1_AndSaveToDisk()
    {
        var preV1ProfilesJson = @"
        [
            {
                ""Name"": ""Standard Profile"",
                ""Wpm"": 150,
                ""Preferences"": {
                    ""TextSize"": 30,
                    ""LineSpacing"": 1.3,
                    ""WindowOpacity"": 1.0,
                    ""AlwaysOnTop"": false,
                    ""FontFamily"": ""Arial"",
                    ""ReadingMargin"": 30
                },
                ""AlwaysOnTop"": false,
                ""ScreenShareSafeMode"": false,
                ""HotkeyGesture"": ""Ctrl+Alt+Space"",
                ""WindowX"": 50,
                ""WindowY"": 50,
                ""WindowWidth"": 800,
                ""WindowHeight"": 600
            }
        ]";
        await File.WriteAllTextAsync(_tempFile, preV1ProfilesJson);

        var store = new ProfileStore(_tempFile);

        var (profiles, recovered) = await store.LoadAsync();
        recovered.Should().BeFalse();
        profiles.Count.Should().Be(1);
        profiles[0].SchemaVersion.Should().Be(1);
        profiles[0].Wpm.Should().Be(150);

        var updatedJson = await File.ReadAllTextAsync(_tempFile);
        var parsedArray = JsonSerializer.Deserialize<JsonElement>(updatedJson);
        parsedArray[0].GetProperty("SchemaVersion").GetInt32().Should().Be(1);
    }
}
