using Avalonia;
using Promptino.Storage.Settings;
using System.Threading.Tasks;

namespace Promptino.App.Services;

public class RemoteWindowBoundsService
{
    private readonly AppSettingsStore _store;

    public RemoteWindowBoundsService(AppSettingsStore store)
    {
        _store = store;
    }

    public async Task<PixelRect> LoadBoundsAsync()
    {
        var load = await _store.LoadAsync();
        var b = load.Settings.EffectiveRemoteWindowBounds;
        return new PixelRect(b.X, b.Y, b.Width, b.Height);
    }

    public async Task SaveBoundsAsync(PixelRect bounds)
    {
        var load = await _store.LoadAsync();
        var s = load.Settings with { RemoteWindowBounds = new WindowBoundsSettings(bounds.X, bounds.Y, bounds.Width, bounds.Height) };
        await _store.SaveAsync(s);
    }
}
