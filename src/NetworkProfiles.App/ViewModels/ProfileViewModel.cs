using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.App.ViewModels;

/// <summary>
/// Editable view of one profile. Raises <see cref="Edited"/> whenever a stored
/// setting changes so the owner can persist it.
/// </summary>
public sealed partial class ProfileViewModel : ObservableObject
{
    private static readonly HashSet<string> DerivedProperties =
    [
        nameof(Errors), nameof(HasErrors), nameof(Summary),
        nameof(IsIpv4Manual), nameof(IsDnsManual), nameof(IsProxyManual), nameof(IsProxyAutoConfig),
    ];

    public static IReadOnlyList<Option<ConfigMode>> Ipv4Modes { get; } =
    [
        new(ConfigMode.Automatic, "Automatic (DHCP)"),
        new(ConfigMode.Manual, "Manual"),
        new(ConfigMode.Keep, "Don't change"),
    ];

    public static IReadOnlyList<Option<ConfigMode>> DnsModes { get; } =
    [
        new(ConfigMode.Automatic, "Automatic (DHCP)"),
        new(ConfigMode.Manual, "Manual"),
        new(ConfigMode.Keep, "Don't change"),
    ];

    public static IReadOnlyList<Option<ProxyMode>> ProxyModes { get; } =
    [
        new(ProxyMode.Keep, "Don't change"),
        new(ProxyMode.Off, "No proxy"),
        new(ProxyMode.Manual, "Manual proxy server"),
        new(ProxyMode.AutoConfig, "Setup script (PAC)"),
    ];

    public Guid Id { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _description;

    [ObservableProperty] private ConfigMode _ipv4Mode;
    [ObservableProperty] private string _gateway;

    /// <summary>Static IPv4 addresses; the first is the primary one.</summary>
    public ObservableCollection<AddressEntryViewModel> Addresses { get; } = [];

    [ObservableProperty] private ConfigMode _dnsMode;

    /// <summary>DNS servers separated by commas, spaces or new lines, in priority order.</summary>
    [ObservableProperty] private string _dnsServers;

    [ObservableProperty] private ProxyMode _proxyMode;
    [ObservableProperty] private string _proxyServer;
    [ObservableProperty] private string _proxyBypassList;
    [ObservableProperty] private bool _proxyBypassLocal;
    [ObservableProperty] private string _proxyAutoConfigUrl;

    public event EventHandler? Edited;

    public ProfileViewModel(NetworkProfile model)
    {
        Id = model.Id;
        _name = model.Name;
        _description = model.Description;
        _ipv4Mode = model.Ipv4.Mode;
        _gateway = model.Ipv4.Gateway;
        _dnsMode = model.Dns.Mode;
        _dnsServers = string.Join(", ", model.Dns.Servers);
        _proxyMode = model.Proxy.Mode;
        _proxyServer = model.Proxy.Server;
        _proxyBypassList = model.Proxy.BypassList;
        _proxyBypassLocal = model.Proxy.BypassLocal;
        _proxyAutoConfigUrl = model.Proxy.AutoConfigUrl;

        foreach (var entry in model.Ipv4.Addresses)
            AddEntry(new AddressEntryViewModel(entry.Address, entry.SubnetMask));
        Addresses.CollectionChanged += (_, _) => NotifyEdited();
    }

    [RelayCommand]
    private void AddAddress() => AddEntry(new AddressEntryViewModel("", Addresses.LastOrDefault()?.SubnetMask ?? "24"));

    [RelayCommand]
    private void RemoveAddress(AddressEntryViewModel entry)
    {
        entry.PropertyChanged -= OnEntryChanged;
        Addresses.Remove(entry);
    }

    private void AddEntry(AddressEntryViewModel entry)
    {
        entry.PropertyChanged += OnEntryChanged;
        Addresses.Add(entry);
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e) => NotifyEdited();

    partial void OnIpv4ModeChanged(ConfigMode value)
    {
        // Switching to manual with nothing entered yet: offer an empty row to fill in.
        if (value == ConfigMode.Manual && Addresses.Count == 0)
            AddAddress();
    }

    public bool IsIpv4Manual => Ipv4Mode == ConfigMode.Manual;
    public bool IsDnsManual => DnsMode == ConfigMode.Manual;
    public bool IsProxyManual => ProxyMode == ProxyMode.Manual;
    public bool IsProxyAutoConfig => ProxyMode == ProxyMode.AutoConfig;

    public IReadOnlyList<string> Errors => ProfileValidator.Validate(ToModel());
    public bool HasErrors => Errors.Count > 0;

    /// <summary>One-line description for the profile list.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                Ipv4Mode switch
                {
                    ConfigMode.Automatic => "DHCP",
                    ConfigMode.Manual => DescribeAddresses(),
                    _ => "IP unchanged",
                },
            };
            if (DnsMode == ConfigMode.Manual && SplitServers(DnsServers).FirstOrDefault() is { } dns)
                parts.Add($"DNS {dns}");
            if (ProxyMode is ProxyMode.Manual or ProxyMode.AutoConfig)
                parts.Add("Proxy");
            else if (ProxyMode == ProxyMode.Off)
                parts.Add("No proxy");
            return string.Join(" · ", parts);
        }
    }

    public NetworkProfile ToModel() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        Description = Description.Trim(),
        Ipv4 = new Ipv4Settings
        {
            Mode = Ipv4Mode,
            Addresses = Addresses.Select(a => a.ToModel()).ToList(),
            Gateway = Gateway.Trim(),
        },
        Dns = new DnsSettings { Mode = DnsMode, Servers = SplitServers(DnsServers) },
        Proxy = new ProxySettings
        {
            Mode = ProxyMode,
            Server = ProxyServer.Trim(),
            BypassList = ProxyBypassList.Trim(),
            BypassLocal = ProxyBypassLocal,
            AutoConfigUrl = ProxyAutoConfigUrl.Trim(),
        },
    };

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && !DerivedProperties.Contains(e.PropertyName))
            NotifyEdited();
    }

    private void NotifyEdited()
    {
        foreach (var derived in DerivedProperties)
            OnPropertyChanged(derived);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private string DescribeAddresses()
    {
        if (Addresses.Count == 0)
            return "No address";
        var first = Addresses[0];
        var text = Ipv4.TryParseMask(first.SubnetMask, out var mask)
            ? $"{first.Address.Trim()}/{Ipv4.ToPrefix(mask)}"
            : first.Address.Trim();
        return Addresses.Count > 1 ? $"{text} +{Addresses.Count - 1}" : text;
    }

    private static List<string> SplitServers(string text) =>
        text.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
}
