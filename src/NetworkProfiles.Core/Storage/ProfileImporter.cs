using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Storage;

public static class ProfileImporter
{
    /// <summary>
    /// Prepares imported profiles for adding to an existing list: profiles
    /// whose id already exists get a new id, and clashing names get a suffix,
    /// so importing never overwrites anything.
    /// </summary>
    public static List<NetworkProfile> PrepareForImport(IEnumerable<NetworkProfile> imported, IEnumerable<NetworkProfile> existing)
    {
        var ids = existing.Select(p => p.Id).ToHashSet();
        var names = existing.Select(p => p.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var result = new List<NetworkProfile>();

        foreach (var profile in imported)
        {
            if (!ids.Add(profile.Id))
            {
                profile.Id = Guid.NewGuid();
                ids.Add(profile.Id);
            }

            var baseName = string.IsNullOrWhiteSpace(profile.Name) ? "Imported profile" : profile.Name.Trim();
            var name = baseName;
            for (var i = 2; names.Contains(name); i++)
                name = $"{baseName} ({i})";
            profile.Name = name;
            names.Add(name);

            result.Add(profile);
        }

        return result;
    }
}
