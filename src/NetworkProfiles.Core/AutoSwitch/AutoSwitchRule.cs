namespace NetworkProfiles.Core.AutoSwitch;

/// <summary>
/// "While this program runs, use that profile."
/// </summary>
public sealed class AutoSwitchRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Executable name, with or without path and ".exe", for example "game.exe".</summary>
    public string Program { get; set; } = "";

    public Guid ProfileId { get; set; }

    /// <summary>Id of the adapter the profile is applied to.</summary>
    public string? AdapterId { get; set; }

    /// <summary>Adapter name when the rule was saved, shown while that adapter is not present.</summary>
    public string? AdapterName { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The comparable process name: file name only, without ".exe", as
    /// reported by <c>Process.ProcessName</c>.
    /// </summary>
    public static string NormalizeProgram(string program)
    {
        var name = program.Trim().Trim('"');
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
            name = name[(slash + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }
}
