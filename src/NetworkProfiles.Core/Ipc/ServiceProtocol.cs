using System.Text.Json;
using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Storage;

namespace NetworkProfiles.Core.Ipc;

/// <summary>
/// Messages between the app and the background service. One JSON request and
/// one JSON response per pipe connection, each on a single line.
/// </summary>
public static class ServiceProtocol
{
    /// <summary>Windows service name, as installed by the MSI and scripts/install-service.ps1.</summary>
    public const string ServiceName = "NetworkProfiles";

    public const string PipeName = "NetworkProfiles.Service.v1";

    /// <summary>Largest request the service reads, to bound memory use.</summary>
    public const int MaxMessageLength = 64 * 1024;

    public static string Serialize<T>(T message) =>
        JsonSerializer.Serialize(message, JsonFile.CompactOptions);

    public static T? Deserialize<T>(string line) =>
        JsonSerializer.Deserialize<T>(line, JsonFile.CompactOptions);
}

public enum ServiceRequestKind
{
    /// <summary>Check that the service is running.</summary>
    Ping,

    /// <summary>Apply a validated profile's adapter settings.</summary>
    Apply,

    /// <summary>Put back settings captured from the adapter before an apply.</summary>
    Restore,
}

public sealed class ServiceRequest
{
    public ServiceRequestKind Kind { get; set; }
    public int InterfaceIndex { get; set; }

    /// <summary>
    /// Only the IPv4 and DNS parts are used; the proxy is per user and is set
    /// by the app itself.
    /// </summary>
    public NetworkProfile? Profile { get; set; }
}

public sealed class ServiceResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = "";
    public int ProtocolVersion { get; set; } = 1;
}
