using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetworkProfiles.Core.Execution;

/// <summary>What an adapter has right now, as far as applying a profile needs to know.</summary>
/// <param name="IsDhcpEnabled">Whether DHCP is on for IPv4.</param>
/// <param name="ManualAddresses">Static IPv4 addresses still assigned (DHCP and link-local ones are left out).</param>
public sealed record InterfaceState(bool IsDhcpEnabled, IReadOnlyList<string> ManualAddresses)
{
    /// <summary>Reads the state of the adapter with this IPv4 interface index; null when it can't be read.</summary>
    public static InterfaceState? Read(int interfaceIndex)
    {
        if (!OperatingSystem.IsWindows())   // DHCP state and address origins are Windows-only
            return null;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!nic.Supports(NetworkInterfaceComponent.IPv4))
                    continue;
                var props = nic.GetIPProperties();
                var v4 = props.GetIPv4Properties();
                if (v4?.Index != interfaceIndex)
                    continue;
                var manual = new List<string>();
                foreach (var address in props.UnicastAddresses)   // a loop, so the Windows check above covers it
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && address.PrefixOrigin == PrefixOrigin.Manual)
                        manual.Add(address.Address.ToString());
                }
                return new InterfaceState(v4.IsDhcpEnabled, manual);
            }
        }
        catch (NetworkInformationException)
        {
        }
        return null;
    }
}
