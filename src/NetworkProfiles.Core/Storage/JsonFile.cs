using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkProfiles.Core.Storage;

/// <summary>
/// Shared JSON settings and atomic file writes for the app's data files.
/// </summary>
internal static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Same as <see cref="Options"/> but on one line, for pipe messages.</summary>
    public static readonly JsonSerializerOptions CompactOptions = new(Options) { WriteIndented = false };

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
            return default;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options);
    }

    /// <summary>
    /// Writes to a temporary file first and then swaps it in, so a crash
    /// mid-write never leaves a truncated file behind.
    /// </summary>
    public static void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, value, Options);
        }
        File.Move(tempPath, path, overwrite: true);
    }

    public static string AppDataPath(string fileName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetworkProfiles",
        fileName);
}
