using FluentAssertions;
using Promptino.App.Services;
using Promptino.Storage.Settings;
using System.IO;
using System.Threading.Tasks;

namespace Promptino.App.Tests.Services;

public class ProfileCoordinatorTests
{
    [Fact]
    public async Task LoadProfilesAsync_WhenStoreIsEmpty_CreatesDefaultProfile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new ProfileStore(tempFile);
            var coordinator = new ProfileCoordinator(store);

            var result = await coordinator.LoadProfilesAsync();

            result.Success.Should().BeTrue();
            coordinator.Profiles.Should().HaveCount(1);
            coordinator.Profiles[0].Name.Should().Be("Default");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SaveProfileAsync_UpdatesExistingProfileAndAddsNew()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new ProfileStore(tempFile);
            var coordinator = new ProfileCoordinator(store);
            await coordinator.LoadProfilesAsync();

            var newProfile = new SavedProfile("MyProfile", 150, new ReadingPreferences(32, 1.5, 1.0, false, "Inter", 40), false, false, "Ctrl+P", 10, 10, 800, 600);
            var result = await coordinator.SaveProfileAsync(newProfile);

            result.Should().BeTrue();
            coordinator.Profiles.Should().Contain(p => p.Name == "MyProfile");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesProfileFromStore()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new ProfileStore(tempFile);
            var coordinator = new ProfileCoordinator(store);
            await coordinator.LoadProfilesAsync();

            var newProfile = new SavedProfile("ToDelete", 150, new ReadingPreferences(32, 1.5, 1.0, false, "Inter", 40), false, false, "Ctrl+P", 10, 10, 800, 600);
            await coordinator.SaveProfileAsync(newProfile);
            
            var result = await coordinator.DeleteProfileAsync("ToDelete");

            result.Should().BeTrue();
            coordinator.Profiles.Should().NotContain(p => p.Name == "ToDelete");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
