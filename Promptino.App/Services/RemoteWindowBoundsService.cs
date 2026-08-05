using Avalonia;
using Promptino.Storage.Settings;
using System.Threading.Tasks;

namespace Promptino.App.Services;

/// <summary>
/// Manages loading and saving window position bounds for the remote mini window.
/// </summary>
public class RemoteWindowBoundsService
{
    private readonly AppSettingsStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteWindowBoundsService"/>.
    /// </summary>
    public RemoteWindowBoundsService(AppSettingsStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Loads remote mini window pixel bounds asynchronously.
    /// </summary>
    public async Task<PixelRect> LoadBoundsAsync()
    {
        var load = await _store.LoadAsync();
        var b = load.Settings.EffectiveRemoteWindowBounds;
        return new PixelRect(b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>
    /// Saves remote mini window pixel bounds to settings storage asynchronously.
    /// </summary>
    public async Task SaveBoundsAsync(PixelRect bounds)
    {
        var load = await _store.LoadAsync();
        var s = load.Settings with { RemoteWindowBounds = new WindowBoundsSettings(bounds.X, bounds.Y, bounds.Width, bounds.Height) };
        await _store.SaveAsync(s);
    }
}
