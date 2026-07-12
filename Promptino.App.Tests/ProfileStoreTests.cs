using Promptino.Storage.Settings;

namespace Promptino.App.Tests;

public class ProfileStoreTests
{
    // ── 10-3: Default Onboarding Profile ─────────────────────────────────────

    [Fact]
    public async Task EnsureDefaultProfile_WhenStoreIsEmpty_SeedsDefaultProfile()
    {
        var path = TestHelpers.TempPath();
        var store = new ProfileStore(path);

        var seeded = await store.EnsureDefaultProfileAsync([]);

        seeded.Should().BeTrue();
        var (profiles, recovered) = await store.LoadAsync();
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("Default");
        recovered.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDefaultProfile_WhenProfilesExist_DoesNotSeed()
    {
        var path = TestHelpers.TempPath();
        var store = new ProfileStore(path);
        var existing = new[] { ProfileStore.CreateDefault() with { Name = "My Setup" } };
        await store.SaveAllAsync(existing);

        var seeded = await store.EnsureDefaultProfileAsync(existing);

        seeded.Should().BeFalse();
        var (profiles, _) = await store.LoadAsync();
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("My Setup");
    }

    [Fact]
    public async Task EnsureDefaultProfile_IsIdempotent()
    {
        var path = TestHelpers.TempPath();
        var store = new ProfileStore(path);

        await store.EnsureDefaultProfileAsync([]);
        var (after1, _) = await store.LoadAsync();
        await store.EnsureDefaultProfileAsync(after1);
        var (after2, _) = await store.LoadAsync();

        after2.Should().HaveCount(1);
    }

    [Fact]
    public void CreateDefault_HasBaselineValues()
    {
        var p = ProfileStore.CreateDefault();

        p.Name.Should().Be("Default");
        p.Wpm.Should().Be(130);
        p.Preferences.TextSize.Should().Be(32);
        p.Preferences.LineSpacing.Should().Be(1.4);
        p.Preferences.WindowOpacity.Should().Be(1.0);
        p.Preferences.FontFamily.Should().Be("Segoe UI");
        p.Preferences.ReadingMargin.Should().Be(40);
        p.AlwaysOnTop.Should().BeFalse();
        p.ScreenShareSafeMode.Should().BeFalse();
        p.HotkeyGesture.Should().Be("Ctrl+Alt+Space");
        p.WindowWidth.Should().BeGreaterThan(0);
        p.WindowHeight.Should().BeGreaterThan(0);
    }


    // ── 10-4: Corrupt Profile Recovery ───────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsEmptyNotRecovered()
    {
        var store = new ProfileStore(TestHelpers.TempPath());

        var (profiles, recovered) = await store.LoadAsync();

        profiles.Should().BeEmpty();
        recovered.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupt_ReturnsEmptyAndRecovered()
    {
        var path = TestHelpers.TempPath();
        await File.WriteAllTextAsync(path, "this is not valid json {{{{");
        var store = new ProfileStore(path);

        var (profiles, recovered) = await store.LoadAsync();

        profiles.Should().BeEmpty();
        recovered.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsValid_ReturnsProfilesNotRecovered()
    {
        var path = TestHelpers.TempPath();
        var store = new ProfileStore(path);
        await store.SaveAllAsync([ProfileStore.CreateDefault()]);

        var (profiles, recovered) = await store.LoadAsync();

        profiles.Should().HaveCount(1);
        recovered.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAllAsync_AfterCorruptFile_OverwritesWithValidContent()
    {
        var path = TestHelpers.TempPath();
        await File.WriteAllTextAsync(path, "corrupt data");
        var store = new ProfileStore(path);

        await store.SaveAllAsync([ProfileStore.CreateDefault()]);
        var (profiles, recovered) = await store.LoadAsync();

        profiles.Should().HaveCount(1);
        recovered.Should().BeFalse();
    }
}
