using System.Text.Json;

namespace Promptino.Storage.Settings;

internal static class JsonStorageOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = IsDebug,
        TypeInfoResolver = StorageJsonContext.Default
    };

    internal static readonly StorageJsonContext Context = new(Default);

#if DEBUG
    private const bool IsDebug = true;
#else
    private const bool IsDebug = false;
#endif
}
