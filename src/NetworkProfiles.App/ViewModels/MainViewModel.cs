using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetworkProfiles.App.Services;
using NetworkProfiles.Core.AutoSwitch;
using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Storage;

namespace NetworkProfiles.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan KeepChangesTimeout = TimeSpan.FromSeconds(15);
    private const string ProfileFileFilter = "Network profiles (*.json)|*.json|All files (*.*)|*.*";

    private readonly ProfileStore _store;
    private readonly string _settingsPath;
    private readonly AppSettings _settings;
    private readonly AdapterService _adapters;
    private readonly ProfileApplier _applier;
    private readonly IDialogService _dialogs;
    private readonly AdapterSettingsWriter _adapterWriter;
    private readonly AutoSwitchEngine _autoSwitch = new();
    private AppliedChange? _autoSwitchChange;

    /// <summary>A restore that waits for its adapter (for example a USB adapter) to be plugged back in.</summary>
    private AppliedChange? _pendingRestore;

    /// <summary>Rules whose "adapter not connected" notice was already shown during this program run.</summary>
    private readonly HashSet<Guid> _waitingForAdapter = [];
    private bool _checkingAutoSwitch;
    private CancellationTokenSource? _pendingSave;

    public ObservableCollection<ProfileViewModel> Profiles { get; } = [];
    public ObservableCollection<AdapterInfo> Adapters { get; } = [];
    public ObservableCollection<RuleViewModel> Rules { get; } = [];

    /// <summary>Adapters rules can use. Only ever grows during a session, see <see cref="AdapterChoice"/>.</summary>
    public ObservableCollection<AdapterChoice> AdapterChoices { get; } = [];

    /// <summary>Raised for events worth a tray notification, such as an automatic switch.</summary>
    public event EventHandler<(string Message, bool IsError)>? Notification;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateProfileCommand), nameof(DeleteProfileCommand), nameof(ApplyCommand))]
    private ProfileViewModel? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(CaptureFromAdapterCommand))]
    private AdapterInfo? _selectedAdapter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _serviceStatus = "";
    [ObservableProperty] private bool _serviceMissing;

    public bool AutoSwitchEnabled
    {
        get => _settings.AutoSwitchEnabled;
        set
        {
            if (_settings.AutoSwitchEnabled == value)
                return;
            _settings.AutoSwitchEnabled = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool StartWithWindows
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            try
            {
                StartupRegistration.SetEnabled(value);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                SetStatus($"Could not change the startup setting: {ex.Message}", isError: true);
            }
            OnPropertyChanged();
        }
    }

    public bool HasSelection => SelectedProfile is not null;

    public AppSettings Settings => _settings;

    /// <summary>The adapter quick-switching from the tray applies to.</summary>
    /// <remarks>Null while that adapter is unplugged; it never silently falls back to another adapter.</remarks>
    public AdapterInfo? TrayAdapter => _settings.LastAdapterId is { } id
        ? Adapters.FirstOrDefault(a => a.Id == id)
        : SelectedAdapter;

    /// <summary>Name of the tray adapter, also while it is unplugged.</summary>
    public string? TrayAdapterName => TrayAdapter?.Name ?? _settings.LastAdapterName;

    public MainViewModel(ProfileStore store, string settingsPath, AdapterService adapters,
        AdapterSettingsWriter adapterWriter, ProfileApplier applier, IDialogService dialogs)
    {
        _store = store;
        _settingsPath = settingsPath;
        _settings = AppSettings.Load(settingsPath);
        _adapters = adapters;
        _adapterWriter = adapterWriter;
        _applier = applier;
        _dialogs = dialogs;
    }

    public void Load()
    {
        try
        {
            foreach (var profile in _store.Load())
                AddProfile(new ProfileViewModel(profile));
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read {_store.FilePath}: {ex.Message}", isError: true);
        }

        foreach (var rule in _settings.AutoSwitchRules)
            AddRule(new RuleViewModel(rule));

        // Rules may point to adapters that are unplugged right now; keep them selectable.
        RefreshAdapters();
        foreach (var rule in Rules)
        {
            if (rule.AdapterId is { } id && AdapterChoices.All(c => c.Id != id))
                AdapterChoices.Add(new AdapterChoice(id, $"{rule.AdapterName ?? "Unknown adapter"} (not connected)"));
        }

        SelectedProfile = Profiles.FirstOrDefault();
        RefreshAdapters();
        if (Adapters.FirstOrDefault(a => a.Id == _settings.LastAdapterId) is { } last)
            SelectedAdapter = last;

        _ = CheckServiceAsync();
    }

    [RelayCommand]
    private async Task CheckServiceAsync()
    {
        var available = await _adapterWriter.IsServiceAvailableAsync();
        ServiceMissing = !available && !AdapterSettingsWriter.IsElevated;
        ServiceStatus = available
            ? "Service running"
            : AdapterSettingsWriter.IsElevated
                ? "Running as administrator (service not installed)"
                : "Service not running: install Network Profiles with its setup, or start the app as administrator, to change adapter settings.";
    }

    [RelayCommand]
    private void RefreshAdapters()
    {
        var selectedId = SelectedAdapter?.Id;
        Adapters.Clear();
        foreach (var adapter in _adapters.GetAdapters())
        {
            Adapters.Add(adapter);
            if (AdapterChoices.All(c => c.Id != adapter.Id))
                AdapterChoices.Add(new AdapterChoice(adapter.Id, adapter.Name));
        }
        SelectedAdapter = Adapters.FirstOrDefault(a => a.Id == selectedId)
            ?? Adapters.FirstOrDefault(a => a.IsUp)
            ?? Adapters.FirstOrDefault();
        OnPropertyChanged(nameof(TrayAdapter));
        OnPropertyChanged(nameof(TrayAdapterName));
    }

    [RelayCommand]
    private void NewProfile()
    {
        var profile = new ProfileViewModel(new NetworkProfile { Name = UniqueName("New profile") });
        AddProfile(profile);
        SelectedProfile = profile;
        ScheduleSave();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateProfile()
    {
        var copy = SelectedProfile!.ToModel();
        copy.Id = Guid.NewGuid();
        copy.Name = UniqueName(copy.Name + " copy");
        var profile = new ProfileViewModel(copy);
        AddProfile(profile);
        SelectedProfile = profile;
        ScheduleSave();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteProfile()
    {
        var profile = SelectedProfile!;
        if (!_dialogs.Confirm($"Delete the profile \"{profile.Name}\"?", "Delete profile"))
            return;

        var index = Profiles.IndexOf(profile);
        profile.Edited -= OnProfileEdited;
        Profiles.Remove(profile);
        SelectedProfile = Profiles.Count == 0 ? null : Profiles[Math.Min(index, Profiles.Count - 1)];
        ScheduleSave();
    }

    [RelayCommand]
    private void ImportProfiles()
    {
        var path = _dialogs.PickFileToOpen("Import profiles", ProfileFileFilter);
        if (path is null)
            return;

        try
        {
            var imported = new ProfileStore(path).Load();
            if (imported.Count == 0)
            {
                SetStatus($"{Path.GetFileName(path)} contains no profiles.", isError: true);
                return;
            }

            var prepared = ProfileImporter.PrepareForImport(imported, Profiles.Select(p => p.ToModel()));
            foreach (var model in prepared)
                AddProfile(new ProfileViewModel(model));
            SelectedProfile = Profiles[^prepared.Count];
            SaveNow();
            SetStatus($"Imported {prepared.Count} profile(s) from {Path.GetFileName(path)}.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    private void ExportProfiles()
    {
        if (Profiles.Count == 0)
        {
            SetStatus("There are no profiles to export yet.", isError: true);
            return;
        }

        var path = _dialogs.PickFileToSave("Export profiles", ProfileFileFilter, "network-profiles.json");
        if (path is null)
            return;

        try
        {
            new ProfileStore(path).Save(Profiles.Select(p => p.ToModel()));
            SetStatus($"Exported {Profiles.Count} profile(s) to {Path.GetFileName(path)}.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    private bool CanCapture() => SelectedAdapter is not null;

    /// <summary>Creates a profile from the selected adapter's current settings.</summary>
    [RelayCommand(CanExecute = nameof(CanCapture))]
    private void CaptureFromAdapter()
    {
        RefreshAdapters();
        if (SelectedAdapter is not { } adapter)
            return;

        var model = ProfileFactory.FromAdapter(adapter, UniqueName(adapter.Name));
        var profile = new ProfileViewModel(model);
        AddProfile(profile);
        SelectedProfile = profile;
        ScheduleSave();
        SetStatus($"Created \"{model.Name}\" from the current settings of {adapter.Name}.", isError: false);
    }

    private bool CanApply() => SelectedProfile is not null && SelectedAdapter is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var profile = SelectedProfile!;
        var adapter = SelectedAdapter!;

        var proxyNote = profile.ProxyMode == ProxyMode.Keep
            ? ""
            : "\n\nThe proxy setting applies to your Windows account, not only this adapter.";
        if (!profile.HasErrors && !_dialogs.Confirm($"Apply \"{profile.Name}\" to {adapter.Name}?{proxyNote}", "Apply profile"))
            return;

        await ApplyProfileAsync(profile, adapter.Id);
    }

    /// <summary>Quick switch from the tray: applies without the first confirmation, still with the keep/revert prompt.</summary>
    public async Task<(string Message, bool IsError)> ApplyFromTrayAsync(Guid profileId)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        var adapter = TrayAdapter;
        if (profile is null)
            return ("That profile no longer exists.", true);
        if (adapter is null)
            return (TrayAdapterName is { } name
                ? $"{name} is not connected. Plug it in, or pick another adapter in the main window."
                : "Pick an adapter in the main window first.", true);
        if (IsBusy)
            return ("Another profile is being applied.", true);

        await ApplyProfileAsync(profile, adapter.Id);
        return (StatusMessage, StatusIsError);
    }

    private async Task ApplyProfileAsync(ProfileViewModel profile, string adapterId)
    {
        if (profile.HasErrors)
        {
            SetStatus("Fix the problems shown in the profile before applying it.", isError: true);
            return;
        }

        // Re-read the adapter so the settings remembered for undo are current.
        RefreshAdapters();
        if (Adapters.FirstOrDefault(a => a.Id == adapterId) is not { } adapter)
        {
            SetStatus("That adapter is no longer available.", isError: true);
            return;
        }

        _settings.LastAdapterId = adapter.Id;
        _settings.LastAdapterName = adapter.Name;
        SaveSettings();
        OnPropertyChanged(nameof(TrayAdapter));
        OnPropertyChanged(nameof(TrayAdapterName));

        // Settings chosen by hand replace any restore still waiting for this adapter.
        if (_pendingRestore?.Adapter.Id == adapter.Id)
            _pendingRestore = null;

        IsBusy = true;
        SetStatus($"Applying \"{profile.Name}\" to {adapter.Name}…", isError: false);
        try
        {
            var result = await _applier.ApplyAsync(profile.ToModel(), adapter);
            if (!result.Succeeded || result.Change is null)
            {
                SetStatus(result.Message, isError: true);
                return;
            }

            // A manual change wins over an active auto-switch rule: don't undo it later.
            _autoSwitch.UserChangedSettings();
            _autoSwitchChange = null;

            var keep = _dialogs.AskKeepChanges(
                $"\"{profile.Name}\" is now active on {adapter.Name}. If you lost your connection, " +
                "wait and the previous settings come back on their own.",
                KeepChangesTimeout);

            if (keep)
            {
                _settings.LastAppliedProfileId = profile.Id;
                SaveSettings();
                SetStatus(result.Message, isError: false);
                return;
            }

            SetStatus("Restoring the previous settings…", isError: false);
            var (deferred, restoreError) = await RestoreOrDeferAsync(result.Change);
            SetStatus(deferred
                ? $"{adapter.Name} was unplugged. Its previous settings come back when it is connected again."
                : restoreError is null
                    ? $"Reverted {adapter.Name} to its previous settings."
                    : $"Could not restore every previous setting: {restoreError}", isError: restoreError is not null);
        }
        catch (Exception ex)
        {
            SetStatus($"Applying failed: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
            RefreshAdapters();
        }
    }

    [RelayCommand]
    private void AddRuleEntry()
    {
        var rule = new RuleViewModel(new AutoSwitchRule
        {
            ProfileId = SelectedProfile?.Id ?? Profiles.FirstOrDefault()?.Id ?? Guid.Empty,
            AdapterId = SelectedAdapter?.Id,
            AdapterName = SelectedAdapter?.Name,
        });
        AddRule(rule);
        SaveRules();
    }

    [RelayCommand]
    private void RemoveRule(RuleViewModel rule)
    {
        rule.PropertyChanged -= OnRuleChanged;
        Rules.Remove(rule);
        SaveRules();
    }

    [RelayCommand]
    private void BrowseProgram(RuleViewModel rule)
    {
        var path = _dialogs.PickFileToOpen("Choose a program", "Programs (*.exe)|*.exe");
        if (path is not null)
            rule.Program = Path.GetFileName(path);
    }

    private void AddRule(RuleViewModel rule)
    {
        rule.PropertyChanged += OnRuleChanged;
        Rules.Add(rule);
    }

    private void OnRuleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RuleViewModel.IsActive))
            return;
        if (e.PropertyName == nameof(RuleViewModel.AdapterId) && sender is RuleViewModel rule
            && Adapters.FirstOrDefault(a => a.Id == rule.AdapterId) is { } adapter)
            rule.AdapterName = adapter.Name;
        SaveRules();
    }

    private void SaveRules()
    {
        _settings.AutoSwitchRules = Rules.Select(r => r.ToModel()).ToList();
        SaveSettings();
    }

    /// <summary>
    /// Looks at the running programs and switches profiles when a rule's
    /// program starts or exits. Called periodically by the app.
    /// </summary>
    public async Task CheckAutoSwitchAsync()
    {
        if (_checkingAutoSwitch || IsBusy)
            return;
        _checkingAutoSwitch = true;
        try
        {
            await TryPendingRestoreAsync();

            var rules = _settings.AutoSwitchEnabled ? _settings.AutoSwitchRules : [];
            var running = GetRunningProgramNames();
            var decision = _autoSwitch.Evaluate(rules, running);

            // A new run of the program may show the "not connected" notice again.
            var runningSet = new HashSet<string>(running, StringComparer.OrdinalIgnoreCase);
            _waitingForAdapter.RemoveWhere(id =>
                rules.FirstOrDefault(r => r.Id == id) is not { } rule
                || !runningSet.Contains(AutoSwitchRule.NormalizeProgram(rule.Program)));
            var program = decision.Rule is { } r ? AutoSwitchRule.NormalizeProgram(r.Program) : "The program";

            switch (decision.Action)
            {
                case AutoSwitchAction.Activate:
                    await ActivateRuleAsync(decision.Rule!, program);
                    break;

                case AutoSwitchAction.Restore when _autoSwitchChange is { } change:
                    _autoSwitchChange = null;
                    IsBusy = true;
                    try
                    {
                        var (deferred, error) = await RestoreOrDeferAsync(change);
                        _settings.LastAppliedProfileId = null;
                        SaveSettings();
                        Notify(deferred
                            ? $"{program} closed. {change.Adapter.Name} is not connected, so its previous settings come back when it is plugged in again."
                            : error is null
                                ? $"{program} closed, so the previous network settings are back."
                                : $"{program} closed, but the previous settings could not all be restored: {error}", error is not null);
                    }
                    finally
                    {
                        IsBusy = false;
                        RefreshAdapters();
                    }
                    break;

                case AutoSwitchAction.Restore:
                case AutoSwitchAction.Release:
                    _autoSwitchChange = null;
                    break;
            }

            foreach (var rule in Rules)
                rule.IsActive = rule.Id == _autoSwitch.ActiveRuleId && !_autoSwitch.Overridden;
        }
        catch (Exception ex)
        {
            Notify($"Auto-switch failed: {ex.Message}", isError: true);
        }
        finally
        {
            _checkingAutoSwitch = false;
        }
    }

    private async Task ActivateRuleAsync(AutoSwitchRule rule, string program)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == rule.ProfileId);

        string? problem = profile is null ? $"{program} started, but its rule points to a profile that no longer exists."
            : profile.HasErrors ? $"{program} started, but the profile \"{profile.Name}\" has problems to fix first."
            : rule.AdapterId is null ? $"{program} started, but its rule has no adapter chosen."
            : null;
        if (problem is not null)
        {
            _autoSwitch.ActivationFailed(rule.Id);
            Notify(problem, isError: true);
            return;
        }

        // Checked without refreshing the visible adapter list, since this repeats
        // every few seconds while a USB adapter is unplugged.
        if (_adapters.GetAdapters().All(a => a.Id != rule.AdapterId))
        {
            // Not an error: keep checking and apply as soon as the adapter shows up.
            _autoSwitch.Deactivate(rule.Id);
            if (_waitingForAdapter.Add(rule.Id))
                Notify($"{program} started, but {rule.AdapterName ?? "the rule's adapter"} is not connected. " +
                       $"\"{profile!.Name}\" is applied as soon as it is plugged in.", isError: false);
            return;
        }

        RefreshAdapters();
        if (Adapters.FirstOrDefault(a => a.Id == rule.AdapterId) is not { } adapter)
        {
            _autoSwitch.Deactivate(rule.Id);
            return;
        }
        _waitingForAdapter.Remove(rule.Id);

        IsBusy = true;
        try
        {
            var result = await _applier.ApplyAsync(profile!.ToModel(), adapter!);
            if (result.Succeeded && result.Change is not null)
            {
                _autoSwitchChange = result.Change;
                _settings.LastAppliedProfileId = profile.Id;
                SaveSettings();
                Notify($"{program} started, so \"{profile.Name}\" is now active on {adapter!.Name}.", isError: false);
            }
            else
            {
                _autoSwitch.ActivationFailed(rule.Id);
                Notify($"{program} started, but \"{profile.Name}\" could not be applied: {result.Message}", isError: true);
            }
        }
        finally
        {
            IsBusy = false;
            RefreshAdapters();
        }
    }

    /// <summary>
    /// Restores a change on the adapter it was made on, looked up again by id
    /// because a re-plugged USB adapter can get a new interface index. When the
    /// adapter is unplugged, the proxy is restored now and the adapter settings
    /// wait until it is back (see <see cref="TryPendingRestoreAsync"/>).
    /// </summary>
    private async Task<(bool Deferred, string? Error)> RestoreOrDeferAsync(AppliedChange change)
    {
        var current = _adapters.GetAdapters().FirstOrDefault(a => a.Id == change.Adapter.Id);
        if (current is null)
        {
            var proxyError = _applier.RestoreProxy(change);
            _pendingRestore = change with { PreviousProxy = null };
            return (true, proxyError);
        }

        return (false, await _applier.RestoreAsync(change, current.InterfaceIndex));
    }

    private async Task TryPendingRestoreAsync()
    {
        if (_pendingRestore is not { } pending)
            return;
        var current = _adapters.GetAdapters().FirstOrDefault(a => a.Id == pending.Adapter.Id);
        if (current is null)
            return;

        _pendingRestore = null;
        IsBusy = true;
        try
        {
            var error = await _applier.RestoreAsync(pending, current.InterfaceIndex);
            Notify(error is null
                ? $"{current.Name} is back, and its previous settings are restored."
                : $"{current.Name} is back, but its previous settings could not all be restored: {error}", error is not null);
        }
        finally
        {
            IsBusy = false;
            RefreshAdapters();
        }
    }

    private static List<string> GetRunningProgramNames()
    {
        var names = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    names.Add(process.ProcessName);
                }
                catch (InvalidOperationException)
                {
                    // Process exited while enumerating.
                }
            }
        }
        return names;
    }

    private void Notify(string message, bool isError)
    {
        SetStatus(message, isError);
        Notification?.Invoke(this, (message, isError));
    }

    /// <summary>Writes pending changes immediately, for example when the app exits.</summary>
    public void SaveNow()
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        try
        {
            _store.Save(Profiles.Select(p => p.ToModel()));
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save profiles: {ex.Message}", isError: true);
        }
    }

    public void SaveSettings()
    {
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are convenience only; losing them is not worth interrupting the user.
        }
    }

    private async void ScheduleSave()
    {
        _pendingSave?.Cancel();
        var cts = _pendingSave = new CancellationTokenSource();
        try
        {
            await Task.Delay(SaveDelay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_pendingSave, cts))
            SaveNow();
    }

    private void AddProfile(ProfileViewModel profile)
    {
        profile.Edited += OnProfileEdited;
        Profiles.Add(profile);
    }

    private void OnProfileEdited(object? sender, EventArgs e) => ScheduleSave();

    private string UniqueName(string baseName)
    {
        var name = baseName;
        for (var i = 2; Profiles.Any(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)); i++)
            name = $"{baseName} {i}";
        return name;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
