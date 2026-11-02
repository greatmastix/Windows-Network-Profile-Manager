using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.App.Services;

/// <summary>
/// Lists the machine's network adapters with their current IPv4 settings.
/// </summary>
public sealed class AdapterService
{
    public IReadOnlyList<AdapterInfo> GetAdapters()
    {
        var adapters = new List<AdapterInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            if (!nic.Supports(NetworkInterfaceComponent.IPv4))
                continue;

            var props = nic.GetIPProperties();
            IPv4InterfaceProperties? v4;
            try
            {
                v4 = props.GetIPv4Properties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }
            if (v4 is null)
                continue;

            var unicast = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            adapters.Add(new AdapterInfo(
                Id: nic.Id,
                InterfaceIndex: v4.Index,
                Name: nic.Name,
                Description: nic.Description,
                IsUp: nic.OperationalStatus == OperationalStatus.Up,
                IsDhcpEnabled: v4.IsDhcpEnabled,
                Addresses: unicast
                    .Select(a => new IpAddressEntry(a.Address.ToString(), a.IPv4Mask.ToString()))
                    .ToList(),
                Gateways: props.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(g => g.Address.ToString())
                    .ToList(),
                DnsServers: props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .ToList(),
                IsDnsStatic: HasStaticDns(nic.Id)));
        }

        return adapters
            .OrderByDescending(a => a.IsUp)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Windows does not expose whether DNS servers came from DHCP, but it keeps
    /// manually entered ones in the adapter's NameServer registry value.
    /// </summary>
    private static bool HasStaticDns(string adapterId)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{adapterId}");
            return key?.GetValue("NameServer") is string servers && !string.IsNullOrWhiteSpace(servers);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
