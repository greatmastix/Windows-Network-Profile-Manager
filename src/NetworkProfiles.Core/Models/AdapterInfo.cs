namespace NetworkProfiles.Core.Models;

/// <summary>
/// Snapshot of a network adapter and its current IPv4 configuration.
/// </summary>
public sealed record AdapterInfo(
    string Id,
    int InterfaceIndex,
    string Name,
    string Description,
    bool IsUp,
    bool IsDhcpEnabled,
    IReadOnlyList<IpAddressEntry> Addresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers,
    bool IsDnsStatic)
{
    public string Summary
    {
        get
        {
            if (!IsUp)
                return "Disconnected";
            var address = Addresses.Count > 0 ? Addresses[0].Address : "no IPv4 address";
            var more = Addresses.Count > 1 ? $" +{Addresses.Count - 1}" : "";
            var source = IsDhcpEnabled ? "DHCP" : "static";
            return $"{address}{more} ({source})";
        }
    }

    /// <summary>One line describing the current settings, for the status area.</summary>
    public string Details
    {
        get
        {
            var addresses = Addresses.Count == 0
                ? "no IPv4 address"
                : string.Join(", ", Addresses.Select(a => $"{a.Address} / {a.SubnetMask}"));
            var gateway = Gateways.Count > 0 ? Gateways[0] : "none";
            var dns = DnsServers.Count > 0 ? string.Join(", ", DnsServers) : "none";
            return $"Now: {addresses}  ·  Gateway {gateway}  ·  DNS {dns}";
        }
    }
}
