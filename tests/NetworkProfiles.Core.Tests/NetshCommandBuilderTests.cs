using NetworkProfiles.Core.Commands;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Tests;

public class NetshCommandBuilderTests
{
    private static string[] Lines(NetworkProfile profile, int index = 12) =>
        NetshCommandBuilder.Build(profile, index).Select(args => string.Join(' ', args)).ToArray();

    [Fact]
    public void AutomaticProfile_UsesDhcpForAddressAndDns()
    {
        Assert.Equal(
        [
            "interface ipv4 set address name=12 source=dhcp",
            "interface ipv4 set dnsservers name=12 source=dhcp",
        ], Lines(new NetworkProfile()));
    }

    [Fact]
    public void StaticProfile_NormalizesMaskAndAddsDnsInOrder()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new(" 10.0.0.5 ", "/8")], Gateway = "10.0.0.1" },
            Dns = new DnsSettings { Mode = ConfigMode.Manual, Servers = ["1.1.1.1", "9.9.9.9", "8.8.8.8"] },
        };

        Assert.Equal(
        [
            "interface ipv4 set address name=12 source=static address=10.0.0.5 mask=255.0.0.0 gateway=10.0.0.1",
            "interface ipv4 set dnsservers name=12 source=static address=1.1.1.1 register=primary validate=no",
            "interface ipv4 add dnsservers name=12 address=9.9.9.9 index=2 validate=no",
            "interface ipv4 add dnsservers name=12 address=8.8.8.8 index=3 validate=no",
        ], Lines(profile));
    }

    [Fact]
    public void StaticProfileWithoutGateway_ClearsGateway()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new("192.168.0.2", "255.255.255.0")] },
            Dns = new DnsSettings { Mode = ConfigMode.Keep },
        };

        Assert.Equal(
            ["interface ipv4 set address name=12 source=static address=192.168.0.2 mask=255.255.255.0 gateway=none"],
            Lines(profile));
    }

    [Fact]
    public void KeepModes_ProduceNoCommands()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Keep },
            Dns = new DnsSettings { Mode = ConfigMode.Keep },
        };
        Assert.Empty(Lines(profile));
    }

    [Fact]
    public void ExtraAddresses_AreAddedAfterTheFirst()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings
            {
                Mode = ConfigMode.Manual,
                Addresses = [new("192.168.0.2", "24"), new("10.1.0.2", "16")],
                Gateway = "192.168.0.1",
            },
            Dns = new DnsSettings { Mode = ConfigMode.Keep },
        };

        Assert.Equal(
        [
            "interface ipv4 set address name=12 source=static address=192.168.0.2 mask=255.255.255.0 gateway=192.168.0.1",
            "interface ipv4 add address name=12 address=10.1.0.2 mask=255.255.0.0",
        ], Lines(profile));
    }

    [Fact]
    public void UnparseableEntries_AreSkipped()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new("junk", "24")] },
            Dns = new DnsSettings { Mode = ConfigMode.Manual, Servers = ["junk"] },
        };
        Assert.Empty(Lines(profile));
    }
}
