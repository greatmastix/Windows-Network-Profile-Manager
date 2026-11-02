using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using NetworkProfiles.Core.Commands;
using NetworkProfiles.Core.Execution;
using NetworkProfiles.Core.Ipc;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.App.Services;

/// <summary>
/// Changes adapter settings (IP addresses, gateway, DNS). Uses the background
/// service when it is installed; otherwise runs netsh directly, which only
/// works when the app itself runs as administrator.
/// </summary>
public sealed class AdapterSettingsWriter(NetworkConfigurator direct)
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(90);

    public static bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Returns an error message, or null on success.</summary>
    public async Task<string?> ApplyAsync(NetworkProfile profile, int interfaceIndex, bool isRestore, CancellationToken cancellationToken = default)
    {
        // Profiles that only change the proxy need no adapter changes, and no rights.
        if (NetshCommandBuilder.Build(profile, interfaceIndex).Count == 0)
            return null;

        var request = new ServiceRequest
        {
            Kind = isRestore ? ServiceRequestKind.Restore : ServiceRequestKind.Apply,
            InterfaceIndex = interfaceIndex,
            Profile = profile,
        };

        var response = await TrySendAsync(request, cancellationToken);
        if (response is not null)
            return response.Succeeded ? null : response.Message;

        if (IsElevated)
            return await direct.ApplyAsync(profile, interfaceIndex, cancellationToken);

        return "The Network Profiles service is not running. Install Network Profiles with its setup, " +
               "or start the app as administrator to change adapter settings.";
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default) =>
        await TrySendAsync(new ServiceRequest { Kind = ServiceRequestKind.Ping }, cancellationToken) is { Succeeded: true };

    /// <summary>Sends a request to the service. Returns null when the service cannot be reached.</summary>
    private static async Task<ServiceResponse?> TrySendAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        using var pipe = new NamedPipeClientStream(".", ServiceProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using (var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connect.CancelAfter(ConnectTimeout);
                await pipe.ConnectAsync(connect.Token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException or UnauthorizedAccessException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ResponseTimeout);

        var bytes = Encoding.UTF8.GetBytes(ServiceProtocol.Serialize(request) + "\n");
        await pipe.WriteAsync(bytes, timeout.Token);
        await pipe.FlushAsync(timeout.Token);

        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var line = await reader.ReadLineAsync(timeout.Token);
        return line is null
            ? new ServiceResponse { Message = "The service closed the connection without answering." }
            : ServiceProtocol.Deserialize<ServiceResponse>(line)
              ?? new ServiceResponse { Message = "The service sent an unreadable answer." };
    }
}
