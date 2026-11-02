using NetworkProfiles.Core.Execution;
using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Core.Commands;

/// <summary>
/// Translates a profile into the netsh invocations that apply it. Each entry
/// is one argument list for netsh.exe. The adapter is addressed by interface
/// index so adapter names with spaces or quotes never need escaping.
/// </summary>
public static class NetshCommandBuilder
{
    /// <remarks>
    /// Normally called with a profile that passed <see cref="ProfileValidator"/>.
    /// Entries that do not parse are skipped rather than passed to netsh.
    /// <paramref name="current"/> is the adapter's state right before applying, when known.
    /// </remarks>
    public static IReadOnlyList<string[]> Build(NetworkProfile profile, int interfaceIndex, InterfaceState? current = null)
    {
        var commands = new List<string[]>();
        var name = $"name={interfaceIndex}";

        switch (profile.Ipv4.Mode)
        {
            case ConfigMode.Automatic when current is { IsDhcpEnabled: true }:
                // netsh refuses "source=dhcp" when DHCP is already on ("DHCP is already enabled"), even if
                // static addresses are left over (Settings then shows "Manual"). Remove those instead; the
                // first delete also drops static default gateways.
                for (var i = 0; i < current.ManualAddresses.Count; i++)
                    commands.Add(i == 0
                        ? ["interface", "ipv4", "delete", "address", name, $"address={current.ManualAddresses[i]}", "gateway=all"]
                        : ["interface", "ipv4", "delete", "address", name, $"address={current.ManualAddresses[i]}"]);
                break;
            case ConfigMode.Automatic:
                commands.Add(["interface", "ipv4", "set", "address", name, "source=dhcp"]);
                break;
            case ConfigMode.Manual:
                var addresses = profile.Ipv4.Addresses
                    .Select(e => Ipv4.TryParseAddress(e.Address, out var a) && Ipv4.TryParseMask(e.SubnetMask, out var m)
                        ? (Address: a.ToString(), Mask: m.ToString())
                        : default)
                    .Where(e => e.Address is not null)
                    .ToList();
                if (addresses.Count == 0)
                    break;
                var gateway = Ipv4.TryParseAddress(profile.Ipv4.Gateway, out var gw) ? gw.ToString() : "none";
                // "set" replaces every existing address; "add" appends the rest.
                commands.Add(["interface", "ipv4", "set", "address", name, "source=static",
                    $"address={addresses[0].Address}", $"mask={addresses[0].Mask}", $"gateway={gateway}"]);
                foreach (var (address, mask) in addresses.Skip(1))
                    commands.Add(["interface", "ipv4", "add", "address", name, $"address={address}", $"mask={mask}"]);
                break;
        }

        switch (profile.Dns.Mode)
        {
            case ConfigMode.Automatic:
                commands.Add(["interface", "ipv4", "set", "dnsservers", name, "source=dhcp"]);
                break;
            case ConfigMode.Manual:
                var servers = profile.Dns.Servers
                    .Select(s => Ipv4.TryParseAddress(s, out var a) ? a.ToString() : null)
                    .OfType<string>()
                    .ToList();
                for (var i = 0; i < servers.Count; i++)
                {
                    commands.Add(i == 0
                        ? ["interface", "ipv4", "set", "dnsservers", name, "source=static",
                            $"address={servers[0]}", "register=primary", "validate=no"]
                        : ["interface", "ipv4", "add", "dnsservers", name,
                            $"address={servers[i]}", $"index={i + 1}", "validate=no"]);
                }
                break;
        }

        return commands;
    }
}
