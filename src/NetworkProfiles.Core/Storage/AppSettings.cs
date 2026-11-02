using NetworkProfiles.Core.AutoSwitch;

namespace NetworkProfiles.Core.Storage;

/// <summary>
/// Small per-user app state that is not part of any profile.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Adapter used for the last apply; the tray menu applies to it.</summary>
    public string? LastAdapterId { get; set; }

    /// <summary>Name of <see cref="LastAdapterId"/>, shown while that adapter is unplugged.</summary>
    public string? LastAdapterName { get; set; }

    /// <summary>Profile applied most recently, shown with a check mark in the tray menu.</summary>
    public Guid? LastAppliedProfileId { get; set; }

    /// <summary>Whether the "still running in the tray" hint was shown already.</summary>
    public bool TrayHintShown { get; set; }

    /// <summary>Master switch for all auto-switch rules.</summary>
    public bool AutoSwitchEnabled { get; set; } = true;

    /// <summary>Program rules, in priority order.</summary>
    public List<AutoSwitchRule> AutoSwitchRules { get; set; } = [];

    public static string DefaultPath => JsonFile.AppDataPath("settings.json");

    public static AppSettings Load(string path)
    {
        try
        {
            return JsonFile.Read<AppSettings>(path) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            // Settings are convenience only; start fresh rather than fail.
            return new AppSettings();
        }
    }

    public void Save(string path) => JsonFile.Write(path, this);
}
