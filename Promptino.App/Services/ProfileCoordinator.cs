using Promptino.Storage.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Promptino.App.Services;

public class ProfileCoordinator
{
    private readonly ProfileStore _profileStore;
    private List<SavedProfile> _profiles = [];

    public IReadOnlyList<SavedProfile> Profiles => _profiles;

    public ProfileCoordinator(ProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

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

    public async Task<bool> SaveProfileAsync(SavedProfile profile)
    {
        _profiles.RemoveAll(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        _profiles.Add(profile);
        return await _profileStore.SaveAllAsync(_profiles);
    }

    public async Task<bool> DeleteProfileAsync(string name)
    {
        _profiles.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return await _profileStore.SaveAllAsync(_profiles);
    }

    public SavedProfile? GetProfile(string name)
    {
        return _profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
