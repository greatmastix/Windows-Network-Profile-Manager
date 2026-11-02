using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Storage;

/// <summary>
/// Loads and saves profiles as a single JSON file. The same format is used
/// for the app's own profile list and for exported files.
/// </summary>
public sealed class ProfileStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public static string DefaultPath => JsonFile.AppDataPath("profiles.json");

    /// <exception cref="InvalidDataException">The file is not a profile file.</exception>
    public List<NetworkProfile> Load()
    {
        ProfileFile? file;
        try
        {
            file = JsonFile.Read<ProfileFile>(FilePath);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException($"{Path.GetFileName(FilePath)} is not a valid profile file.", ex);
        }

        var profiles = file?.Profiles ?? [];
        foreach (var profile in profiles)
            profile.Ipv4.MigrateLegacyAddress();
        return profiles;
    }

    public void Save(IEnumerable<NetworkProfile> profiles) =>
        JsonFile.Write(FilePath, new ProfileFile { Profiles = profiles.ToList() });

    private sealed class ProfileFile
    {
        public int Version { get; set; } = 2;
        public List<NetworkProfile> Profiles { get; set; } = [];
    }
}
