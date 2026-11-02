using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.App.Services;

/// <summary>
/// What an apply changed, and how to put it back.
/// </summary>
public sealed record AppliedChange(AdapterInfo Adapter, NetworkProfile PreviousAdapterSettings, ProxyState? PreviousProxy);

public sealed record ApplyResult(bool Succeeded, string Message, AppliedChange? Change);

/// <summary>
/// Applies a profile to one adapter (IPv4 addresses and gateway, DNS, then
/// proxy) after remembering the current settings. If any step fails, the
/// previous settings are restored straight away.
/// </summary>
public sealed class ProfileApplier(AdapterSettingsWriter adapterWriter, ProxyService proxy)
{
    public async Task<ApplyResult> ApplyAsync(NetworkProfile profile, AdapterInfo adapter, CancellationToken cancellationToken = default)
    {
        var errors = ProfileValidator.Validate(profile);
        if (errors.Count > 0)
            return new ApplyResult(false, string.Join(Environment.NewLine, errors), null);

        var change = Snapshot(profile, adapter);

        var error = await adapterWriter.ApplyAsync(profile, adapter.InterfaceIndex, isRestore: false, cancellationToken);
        if (error is null)
        {
            try
            {
                proxy.Apply(profile.Proxy);
            }
            catch (Exception ex)
            {
                error = $"The proxy could not be set: {ex.Message}";
            }
        }

        if (error is null)
            return new ApplyResult(true, $"Applied \"{profile.Name}\" to {adapter.Name}.", change);

        var restoreError = await RestoreAsync(change, adapter.InterfaceIndex, CancellationToken.None);
        var outcome = restoreError is null
            ? "Your previous settings were restored."
            : $"Restoring the previous settings also failed: {restoreError}";
        return new ApplyResult(false, $"{error}{Environment.NewLine}{outcome}", null);
    }

    /// <summary>
    /// Puts back the settings an apply replaced. Returns an error message, or null on success.
    /// </summary>
    /// <param name="interfaceIndex">
    /// The adapter's current index. It can change when a USB adapter is unplugged
    /// and plugged back in, so callers look it up again by adapter id.
    /// </param>
    public async Task<string?> RestoreAsync(AppliedChange change, int interfaceIndex, CancellationToken cancellationToken = default)
    {
        // The snapshot came from the adapter itself, so it is replayed without
        // validation; the command builder skips anything it cannot parse.
        var error = await adapterWriter.ApplyAsync(change.PreviousAdapterSettings, interfaceIndex, isRestore: true, cancellationToken);
        if (RestoreProxy(change) is { } proxyError)
            error = (error is null ? "" : error + Environment.NewLine) + proxyError;
        return error;
    }

    /// <summary>Puts back only the proxy part of a change. Returns an error message, or null.</summary>
    public string? RestoreProxy(AppliedChange change)
    {
        if (change.PreviousProxy is null)
            return null;
        try
        {
            proxy.Write(change.PreviousProxy);
            return null;
        }
        catch (Exception ex)
        {
            return $"The proxy could not be restored: {ex.Message}";
        }
    }

    private AppliedChange Snapshot(NetworkProfile profile, AdapterInfo adapter)
    {
        // Only remember the parts this profile is going to change.
        var previous = ProfileFactory.FromAdapter(adapter, "Previous settings");
        if (profile.Ipv4.Mode == ConfigMode.Keep)
            previous.Ipv4 = new Ipv4Settings { Mode = ConfigMode.Keep };
        if (profile.Dns.Mode == ConfigMode.Keep)
            previous.Dns = new DnsSettings { Mode = ConfigMode.Keep };

        var previousProxy = profile.Proxy.Mode == ProxyMode.Keep ? null : proxy.Read();
        return new AppliedChange(adapter, previous, previousProxy);
    }
}
