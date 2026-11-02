using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Core.Tests;

public class ProfileValidatorTests
{
    private static NetworkProfile StaticProfile(string address = "192.168.1.10", string mask = "24", string gateway = "192.168.1.1") => new()
    {
        Name = "Office",
        Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new(address, mask)], Gateway = gateway },
        Dns = new DnsSettings { Mode = ConfigMode.Manual, Servers = ["1.1.1.1", "8.8.8.8"] },
    };

    [Fact]
    public void DefaultProfile_IsValid() =>
        Assert.Empty(ProfileValidator.Validate(new NetworkProfile()));

    [Fact]
    public void StaticProfile_IsValid() =>
        Assert.Empty(ProfileValidator.Validate(StaticProfile()));

    [Fact]
    public void EmptyGateway_IsAllowed() =>
        Assert.Empty(ProfileValidator.Validate(StaticProfile(gateway: "")));

    [Fact]
    public void MissingName_IsRejected() =>
        Assert.Single(ProfileValidator.Validate(new NetworkProfile { Name = " " }));

    [Fact]
    public void GatewayOutsideSubnet_IsRejected() =>
        Assert.Contains(ProfileValidator.Validate(StaticProfile(gateway: "192.168.2.1")), e => e.Contains("same subnet"));

    [Fact]
    public void GatewayEqualToAddress_IsRejected() =>
        Assert.Contains(ProfileValidator.Validate(StaticProfile(gateway: "192.168.1.10")), e => e.Contains("own IP"));

    [Fact]
    public void BroadcastAddress_IsRejected() =>
        Assert.Contains(ProfileValidator.Validate(StaticProfile(address: "192.168.1.255")), e => e.Contains("broadcast"));

    [Fact]
    public void BadMask_IsRejected() =>
        Assert.Contains(ProfileValidator.Validate(StaticProfile(mask: "255.0.255.0")), e => e.Contains("Subnet mask"));

    [Fact]
    public void ManualDnsWithoutServers_IsRejected()
    {
        var profile = new NetworkProfile { Dns = new DnsSettings { Mode = ConfigMode.Manual } };
        Assert.Single(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void InvalidDnsServer_IsRejected()
    {
        var profile = new NetworkProfile { Dns = new DnsSettings { Mode = ConfigMode.Manual, Servers = ["1.1.1.1", "dns.example"] } };
        Assert.Contains(ProfileValidator.Validate(profile), e => e.Contains("dns.example"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("proxy example:8080")]
    public void ManualProxy_RequiresServer(string server)
    {
        var profile = new NetworkProfile { Proxy = new ProxySettings { Mode = ProxyMode.Manual, Server = server } };
        Assert.Single(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void AutoConfigProxy_RequiresUrl()
    {
        var profile = new NetworkProfile { Proxy = new ProxySettings { Mode = ProxyMode.AutoConfig, AutoConfigUrl = "wpad" } };
        Assert.Single(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void KeepModes_IgnoreStoredValues()
    {
        var profile = new NetworkProfile
        {
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Keep, Addresses = [new("junk", "")] },
            Dns = new DnsSettings { Mode = ConfigMode.Keep, Servers = ["junk"] },
            Proxy = new ProxySettings { Mode = ProxyMode.Keep, Server = "" },
        };
        Assert.Empty(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void ManualIpWithoutAddresses_IsRejected()
    {
        var profile = new NetworkProfile { Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual } };
        Assert.Contains(ProfileValidator.Validate(profile), e => e.Contains("at least one IP"));
    }

    [Fact]
    public void SecondAddressInOtherSubnet_IsValid_AndGatewayMayMatchEither()
    {
        var profile = StaticProfile(gateway: "10.0.0.1");
        profile.Ipv4.Addresses.Add(new("10.0.0.5", "255.255.255.0"));
        Assert.Empty(ProfileValidator.Validate(profile));
    }

    [Fact]
    public void ErrorsNameTheAddressWhenThereAreSeveral()
    {
        var profile = StaticProfile();
        profile.Ipv4.Addresses.Add(new("10.0.0.300", "24"));
        Assert.Contains(ProfileValidator.Validate(profile), e => e.StartsWith("Address 2:"));
    }

    [Fact]
    public void DuplicateAddress_IsRejected()
    {
        var profile = StaticProfile();
        profile.Ipv4.Addresses.Add(new("192.168.1.10", "24"));
        Assert.Contains(ProfileValidator.Validate(profile), e => e.Contains("more than once"));
    }
}
