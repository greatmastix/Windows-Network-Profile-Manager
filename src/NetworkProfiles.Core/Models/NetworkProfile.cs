using System.Text.Json.Serialization;

namespace NetworkProfiles.Core.Models;

/// <summary>
/// How a profile treats one group of settings when it is applied.
/// </summary>
public enum ConfigMode
{
    /// <summary>Leave the adapter's current setting untouched.</summary>
    Keep,

    /// <summary>Obtain the setting automatically (DHCP).</summary>
    Automatic,

    /// <summary>Use the values stored in the profile.</summary>
    Manual,
}

public enum ProxyMode
{
    /// <summary>Leave the current proxy configuration untouched.</summary>
    Keep,

    /// <summary>Connect directly, no proxy.</summary>
    Off,

    /// <summary>Use a fixed proxy server.</summary>
    Manual,

    /// <summary>Use a proxy auto-config (PAC) script.</summary>
    AutoConfig,
}

/// <summary>
/// A named set of network settings. Profiles are not tied to an adapter;
/// the adapter is chosen when the profile is applied.
/// </summary>
public sealed class NetworkProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New profile";
    public string Description { get; set; } = "";
    public Ipv4Settings Ipv4 { get; set; } = new();
    public DnsSettings Dns { get; set; } = new();
    public ProxySettings Proxy { get; set; } = new();
}

public sealed class Ipv4Settings
{
    public ConfigMode Mode { get; set; } = ConfigMode.Automatic;

    /// <summary>Static addresses; the first one is the primary address.</summary>
    public List<IpAddressEntry> Addresses { get; set; } = [];

    /// <summary>Optional default gateway.</summary>
    public string Gateway { get; set; } = "";

    /// <summary>Single address from profile files written before multiple addresses existed.</summary>
    [JsonPropertyName("address")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyAddress { get; set; }

    /// <summary>Subnet mask belonging to <see cref="LegacyAddress"/>.</summary>
    [JsonPropertyName("subnetMask")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacySubnetMask { get; set; }

    /// <summary>Moves a single address from an older profile file into <see cref="Addresses"/>.</summary>
    public void MigrateLegacyAddress()
    {
        if (Addresses.Count == 0 && !string.IsNullOrWhiteSpace(LegacyAddress))
            Addresses.Add(new IpAddressEntry(LegacyAddress, LegacySubnetMask ?? ""));
        LegacyAddress = null;
        LegacySubnetMask = null;
    }
}

/// <summary>An IPv4 address with its subnet mask (dotted, like 255.255.255.0, or a prefix length, like 24).</summary>
public sealed record IpAddressEntry(string Address, string SubnetMask);

public sealed class DnsSettings
{
    public ConfigMode Mode { get; set; } = ConfigMode.Automatic;
    public List<string> Servers { get; set; } = [];
}

/// <summary>
/// Proxy settings. On Windows the system proxy is per user, not per adapter,
/// so applying these affects every connection of the current user.
/// </summary>
public sealed class ProxySettings
{
    public ProxyMode Mode { get; set; } = ProxyMode.Keep;

    /// <summary>host:port, or per-protocol form such as http=host:80;https=host:443.</summary>
    public string Server { get; set; } = "";

    /// <summary>Semicolon separated hosts that skip the proxy.</summary>
    public string BypassList { get; set; } = "";

    /// <summary>Skip the proxy for local (intranet) addresses.</summary>
    public bool BypassLocal { get; set; } = true;

    public string AutoConfigUrl { get; set; } = "";
}
