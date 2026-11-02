using NetworkProfiles.Core.Commands;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.Core.Execution;

/// <summary>
/// Applies the adapter part of a profile (IPv4 addresses, gateway, DNS) by
/// running netsh. Needs administrator rights; the background service uses it,
/// and so does the app when it runs elevated without the service.
/// </summary>
public sealed class NetworkConfigurator(NetshRunner netsh)
{
    /// <summary>Returns an error message, or null when every command succeeded.</summary>
    public async Task<string?> ApplyAsync(NetworkProfile profile, int interfaceIndex, CancellationToken cancellationToken = default)
    {
        // Only switching to DHCP depends on what the adapter has now.
        var current = profile.Ipv4.Mode == ConfigMode.Automatic ? InterfaceState.Read(interfaceIndex) : null;
        foreach (var arguments in NetshCommandBuilder.Build(profile, interfaceIndex, current))
        {
            var result = await netsh.RunAsync(arguments, cancellationToken);
            if (!result.Succeeded)
            {
                var detail = string.IsNullOrWhiteSpace(result.Output) ? $"exit code {result.ExitCode}" : result.Output;
                return $"netsh {string.Join(' ', arguments)} failed: {detail}";
            }
        }
        return null;
    }
}
