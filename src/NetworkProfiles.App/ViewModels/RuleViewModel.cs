using CommunityToolkit.Mvvm.ComponentModel;
using NetworkProfiles.Core.AutoSwitch;

namespace NetworkProfiles.App.ViewModels;

public sealed partial class RuleViewModel : ObservableObject
{
    public Guid Id { get; }

    [ObservableProperty] private string _program;
    [ObservableProperty] private Guid _profileId;
    [ObservableProperty] private string? _adapterId;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isActive;

    /// <summary>Remembered adapter name, for when the adapter is unplugged.</summary>
    public string? AdapterName { get; set; }

    public RuleViewModel(AutoSwitchRule rule)
    {
        _adapterId = rule.AdapterId;
        AdapterName = rule.AdapterName;
        Id = rule.Id;
        _program = rule.Program;
        _profileId = rule.ProfileId;
        _enabled = rule.Enabled;
    }

    public AutoSwitchRule ToModel() => new()
    {
        Id = Id,
        Program = Program.Trim(),
        ProfileId = ProfileId,
        AdapterId = AdapterId,
        AdapterName = AdapterName,
        Enabled = Enabled,
    };
}
