using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Core.Models;

public static class ProfileFactory
{
    /// <summary>
    /// Builds a profile that reproduces an adapter's current IPv4 and DNS
    /// settings. Used both to capture a new profile and to remember the
    /// previous state before applying one, so it can be restored.
    /// Settings that cannot be reproduced reliably are set to Keep.
    /// </summary>
    public static NetworkProfile FromAdapter(AdapterInfo adapter, string name)
    {
        var profile = new NetworkProfile
        {
            Name = name,
            Description = $"Captured from {adapter.Description}",
            Proxy = new ProxySettings { Mode = ProxyMode.Keep },
        };

        if (adapter.IsDhcpEnabled)
        {
            profile.Ipv4 = new Ipv4Settings { Mode = ConfigMode.Automatic };
        }
        else
        {
            var addresses = adapter.Addresses
                .Where(a => Ipv4.TryParseAddress(a.Address, out _) && Ipv4.TryParseMask(a.SubnetMask, out _))
                .ToList();
            profile.Ipv4 = addresses.Count == 0
                ? new Ipv4Settings { Mode = ConfigMode.Keep }
                : new Ipv4Settings
                {
                    Mode = ConfigMode.Manual,
                    Addresses = addresses,
                    Gateway = adapter.Gateways.FirstOrDefault(g => Ipv4.TryParseAddress(g, out _)) ?? "",
                };
        }

        var dnsServers = adapter.DnsServers.Where(d => Ipv4.TryParseAddress(d, out _)).ToList();
        profile.Dns = !adapter.IsDnsStatic
            ? new DnsSettings { Mode = ConfigMode.Automatic }
            : dnsServers.Count > 0
                ? new DnsSettings { Mode = ConfigMode.Manual, Servers = dnsServers }
                : new DnsSettings { Mode = ConfigMode.Keep };

        return profile;
    }
}
