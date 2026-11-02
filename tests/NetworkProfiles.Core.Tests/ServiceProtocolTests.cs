using NetworkProfiles.Core.Ipc;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Tests;

public class ServiceProtocolTests
{
    [Fact]
    public void Request_RoundTripsOnOneLine()
    {
        var request = new ServiceRequest
        {
            Kind = ServiceRequestKind.Apply,
            InterfaceIndex = 12,
            Profile = new NetworkProfile
            {
                Name = "Lab",
                Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new("10.0.0.2", "24")] },
            },
        };

        var line = ServiceProtocol.Serialize(request);
        Assert.DoesNotContain('\n', line);

        var back = ServiceProtocol.Deserialize<ServiceRequest>(line)!;
        Assert.Equal(ServiceRequestKind.Apply, back.Kind);
        Assert.Equal(12, back.InterfaceIndex);
        Assert.Equal("10.0.0.2", back.Profile!.Ipv4.Addresses[0].Address);
    }
}
