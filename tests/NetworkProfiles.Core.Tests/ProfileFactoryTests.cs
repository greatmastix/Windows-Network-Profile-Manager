using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Core.Tests;

public class ProfileFactoryTests
{
    private static AdapterInfo Adapter(
        bool dhcp = false,
        IpAddressEntry[]? addresses = null,
        string[]? gateways = null,
        string[]? dns = null,
        bool dnsStatic = true) => new(
            Id: "{id}", InterfaceIndex: 7, Name: "Ethernet", Description: "Test NIC",
            IsUp: true, IsDhcpEnabled: dhcp,
            Addresses: addresses ?? [new("192.168.1.10", "255.255.255.0"), new("10.0.0.10", "255.0.0.0")],
            Gateways: gateways ?? ["192.168.1.1"],
            DnsServers: dns ?? ["1.1.1.1", "8.8.8.8"],
            IsDnsStatic: dnsStatic);

    [Fact]
    public void StaticAdapter_CapturesAllAddressesGatewayAndDns()
    {
        var profile = ProfileFactory.FromAdapter(Adapter(), "Snap");

        Assert.Equal(ConfigMode.Manual, profile.Ipv4.Mode);
        Assert.Equal(2, profile.Ipv4.Addresses.Count);
        Assert.Equal("192.168.1.1", profile.Ipv4.Gateway);
        Assert.Equal(ConfigMode.Manual, profile.Dns.Mode);
        Assert.Equal(["1.1.1.1", "8.8.8.8"], profile.Dns.Servers);
        Assert.Equal(ProxyMode.Keep, profile.Proxy.Mode);
        Assert.Empty(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void DhcpAdapter_WithDhcpDns_IsFullyAutomatic()
    {
        var profile = ProfileFactory.FromAdapter(Adapter(dhcp: true, dnsStatic: false), "Snap");
        Assert.Equal(ConfigMode.Automatic, profile.Ipv4.Mode);
        Assert.Equal(ConfigMode.Automatic, profile.Dns.Mode);
    }

    [Fact]
    public void DhcpAdapter_WithStaticDns_KeepsDnsServers()
    {
        var profile = ProfileFactory.FromAdapter(Adapter(dhcp: true, dnsStatic: true, dns: ["9.9.9.9"]), "Snap");
        Assert.Equal(ConfigMode.Automatic, profile.Ipv4.Mode);
        Assert.Equal(["9.9.9.9"], profile.Dns.Servers);
    }

    [Fact]
    public void StaticAdapterWithoutAddresses_KeepsIpUntouched()
    {
        var profile = ProfileFactory.FromAdapter(Adapter(addresses: []), "Snap");
        Assert.Equal(ConfigMode.Keep, profile.Ipv4.Mode);
    }

    [Fact]
    public void StaticDnsWithoutServers_KeepsDnsUntouched()
    {
        var profile = ProfileFactory.FromAdapter(Adapter(dns: []), "Snap");
        Assert.Equal(ConfigMode.Keep, profile.Dns.Mode);
    }
}
