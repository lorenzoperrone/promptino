using Promptino.Storage.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Promptino.App.Services;

/// <summary>
/// Coordinates in-memory profile lists and delegates persistence operations to <see cref="ProfileStore"/>.
/// </summary>
public class ProfileCoordinator
{
    private readonly ProfileStore _profileStore;
    private List<SavedProfile> _profiles = [];

    /// <summary>Gets the current in-memory collection of saved profiles.</summary>
    public IReadOnlyList<SavedProfile> Profiles => _profiles;

    /// <summary>
    /// Initializes a new instance of <see cref="ProfileCoordinator"/>.
    /// </summary>
    public ProfileCoordinator(ProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    /// <summary>
    /// Loads profiles asynchronously, populating default profiles if empty.
    /// </summary>
    public async Task<(bool Success, bool Recovered)> LoadProfilesAsync()
    {
        var load = await _profileStore.LoadAsync();
        _profiles = load.Profiles.ToList();

        if (_profiles.Count == 0)
        {
            var created = await _profileStore.EnsureDefaultProfileAsync(_profiles);
            if (created)
            {
                _profiles = [ProfileStore.CreateDefault()];
            }
        }

        return (true, load.Recovered);
    }

    /// <summary>
    /// Saves or updates the specified profile in the collection and persists to storage.
    /// </summary>
    public async Task<bool> SaveProfileAsync(SavedProfile profile)
    {
        _profiles.RemoveAll(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        _profiles.Add(profile);
        return await _profileStore.SaveAllAsync(_profiles);
    }

    /// <summary>
    /// Deletes the profile with the specified name and persists changes.
    /// </summary>
    public async Task<bool> DeleteProfileAsync(string name)
    {
        _profiles.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return await _profileStore.SaveAllAsync(_profiles);
    }

    /// <summary>
    /// Retrieves a saved profile by name, or null if not found.
    /// </summary>
    public SavedProfile? GetProfile(string name)
    {
        return _profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
