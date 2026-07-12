using Avalonia;
using FluentAssertions;
using Promptino.App.Services;
using Promptino.Storage.Settings;
using System.IO;
using System.Threading.Tasks;

namespace Promptino.App.Tests.Services;

public class RemoteWindowBoundsServiceTests
{
    [Fact]
    public async Task SaveAndLoadBounds_PersistsCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AppSettingsStore(tempFile);
            var service = new RemoteWindowBoundsService(store);

            var expectedBounds = new PixelRect(50, 60, 400, 300);
            await service.SaveBoundsAsync(expectedBounds);

            var loadedBounds = await service.LoadBoundsAsync();

            loadedBounds.Should().Be(expectedBounds);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
