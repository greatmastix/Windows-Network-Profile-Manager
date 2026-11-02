using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Validation;

public static class ProfileValidator
{
    /// <summary>
    /// Returns a list of human readable problems. An empty list means the
    /// profile can be saved and applied.
    /// </summary>
    public static IReadOnlyList<string> Validate(NetworkProfile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Name))
            errors.Add("Give the profile a name.");

        ValidateIpv4(profile.Ipv4, errors);
        ValidateDns(profile.Dns, errors);
        ValidateProxy(profile.Proxy, errors);

        return errors;
    }

    private static void ValidateIpv4(Ipv4Settings ipv4, List<string> errors)
    {
        if (ipv4.Mode != ConfigMode.Manual)
            return;

        if (ipv4.Addresses.Count == 0)
            errors.Add("Add at least one IP address, or set the IP address to automatic.");

        var valid = new List<(System.Net.IPAddress Address, System.Net.IPAddress Mask)>();
        for (var i = 0; i < ipv4.Addresses.Count; i++)
        {
            var entry = ipv4.Addresses[i];
            var label = ipv4.Addresses.Count > 1 ? $"Address {i + 1}: " : "";
            var addressOk = Ipv4.TryParseAddress(entry.Address, out var address);
            var maskOk = Ipv4.TryParseMask(entry.SubnetMask, out var mask);

            if (!addressOk)
                errors.Add($"{label}IP address must be a valid IPv4 address, for example 192.168.1.10.");
            if (!maskOk)
                errors.Add($"{label}Subnet mask must be like 255.255.255.0 or a prefix length like 24.");
            if (!addressOk || !maskOk)
                continue;

            if (Ipv4.IsNetworkOrBroadcast(address, mask))
                errors.Add($"{label}IP address is the network or broadcast address of its subnet.");
            else if (valid.Any(v => v.Address.Equals(address)))
                errors.Add($"{label}{address} is listed more than once.");
            else
                valid.Add((address, mask));
        }

        if (string.IsNullOrWhiteSpace(ipv4.Gateway))
            return;

        if (!Ipv4.TryParseAddress(ipv4.Gateway, out var gateway))
            errors.Add("Gateway must be a valid IPv4 address, or left empty.");
        else if (valid.Any(v => v.Address.Equals(gateway)))
            errors.Add("Gateway cannot be one of the profile's own IP addresses.");
        else if (valid.Count > 0 && !valid.Any(v => Ipv4.SameSubnet(v.Address, gateway, v.Mask)))
            errors.Add("Gateway is not in the same subnet as any of the IP addresses.");
    }

    private static void ValidateDns(DnsSettings dns, List<string> errors)
    {
        if (dns.Mode != ConfigMode.Manual)
            return;

        if (dns.Servers.Count == 0)
            errors.Add("Add at least one DNS server, or set DNS to automatic.");

        foreach (var server in dns.Servers)
        {
            if (!Ipv4.TryParseAddress(server, out _))
                errors.Add($"DNS server \"{server}\" is not a valid IPv4 address.");
        }
    }

    private static void ValidateProxy(ProxySettings proxy, List<string> errors)
    {
        switch (proxy.Mode)
        {
            case ProxyMode.Manual when string.IsNullOrWhiteSpace(proxy.Server):
                errors.Add("Enter a proxy server, for example proxy.example.com:8080.");
                break;
            case ProxyMode.Manual when proxy.Server.Any(char.IsWhiteSpace):
                errors.Add("Proxy server cannot contain spaces.");
                break;
            case ProxyMode.AutoConfig when !Uri.TryCreate(proxy.AutoConfigUrl.Trim(), UriKind.Absolute, out _):
                errors.Add("Auto-config script must be a full URL, for example http://wpad/wpad.dat.");
                break;
        }
    }
}
