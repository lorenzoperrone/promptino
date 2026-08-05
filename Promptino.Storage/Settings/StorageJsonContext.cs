using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Promptino.Storage.Settings;

[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<SavedProfile>))]
[JsonSerializable(typeof(List<RecentFileEntry>))]
internal partial class StorageJsonContext : JsonSerializerContext
{
}
