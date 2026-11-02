using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Core.Tests;

public class Ipv4Tests
{
    [Theory]
    [InlineData("192.168.1.10", true)]
    [InlineData(" 10.0.0.1 ", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("10.1", false)]
    [InlineData("256.1.1.1", false)]
    [InlineData("010.0.0.1", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("1.2.3.", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("::1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParseAddress(string? text, bool expected) =>
        Assert.Equal(expected, Ipv4.TryParseAddress(text, out _));

    [Theory]
    [InlineData("255.255.255.0", "255.255.255.0")]
    [InlineData("24", "255.255.255.0")]
    [InlineData("/16", "255.255.0.0")]
    [InlineData("32", "255.255.255.255")]
    [InlineData("255.255.254.0", "255.255.254.0")]
    public void TryParseMask_Valid(string text, string expected)
    {
        Assert.True(Ipv4.TryParseMask(text, out var mask));
        Assert.Equal(expected, mask.ToString());
    }

    [Theory]
    [InlineData("255.0.255.0")]
    [InlineData("0.0.0.0")]
    [InlineData("0")]
    [InlineData("33")]
    [InlineData("")]
    [InlineData("abc")]
    public void TryParseMask_Invalid(string text) =>
        Assert.False(Ipv4.TryParseMask(text, out _));

    [Fact]
    public void ToPrefix_CountsMaskBits()
    {
        Assert.True(Ipv4.TryParseMask("255.255.240.0", out var mask));
        Assert.Equal(20, Ipv4.ToPrefix(mask));
    }

    [Theory]
    [InlineData("192.168.1.0", "24", true)]
    [InlineData("192.168.1.255", "24", true)]
    [InlineData("192.168.1.1", "24", false)]
    [InlineData("10.0.0.0", "31", false)]
    public void IsNetworkOrBroadcast(string address, string mask, bool expected)
    {
        Ipv4.TryParseAddress(address, out var a);
        Ipv4.TryParseMask(mask, out var m);
        Assert.Equal(expected, Ipv4.IsNetworkOrBroadcast(a, m));
    }
}
