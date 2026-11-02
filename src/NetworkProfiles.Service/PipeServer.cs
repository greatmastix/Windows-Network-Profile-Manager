using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using NetworkProfiles.Core.Ipc;

namespace NetworkProfiles.Service;

/// <summary>
/// Listens on the local named pipe and answers one request per connection.
/// </summary>
public sealed class PipeServer(RequestHandler handler, ILogger<PipeServer> logger) : BackgroundService
{
    public const string ServiceName = ServiceProtocol.ServiceName;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Listening on pipe {Pipe}.", ServiceProtocol.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not open the pipe; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                continue;
            }

            // Handle the connection in the background so the next client can connect.
            _ = ServeAsync(pipe, stoppingToken);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        await using var _ = pipe;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            var line = await ReadLineAsync(pipe, timeout.Token);
            ServiceResponse response;
            if (line is null)
            {
                response = new ServiceResponse { Message = "The request was empty or too long." };
            }
            else
            {
                ServiceRequest? request = null;
                try
                {
                    request = ServiceProtocol.Deserialize<ServiceRequest>(line);
                }
                catch (JsonException)
                {
                }
                response = request is null
                    ? new ServiceResponse { Message = "The request could not be read." }
                    : await handler.HandleAsync(request, timeout.Token);
            }

            var bytes = Encoding.UTF8.GetBytes(ServiceProtocol.Serialize(response) + "\n");
            await pipe.WriteAsync(bytes, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            pipe.WaitForPipeDrain();
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            logger.LogDebug(ex, "Client disconnected or timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while handling a request.");
        }
    }

    /// <summary>Reads one UTF-8 line, giving up past <see cref="ServiceProtocol.MaxMessageLength"/> bytes.</summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (buffer.Length <= ServiceProtocol.MaxMessageLength)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            if (newline >= 0)
            {
                buffer.Write(chunk, 0, newline);
                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
            buffer.Write(chunk, 0, read);
        }
        return null;
    }

    /// <summary>
    /// Only interactively logged-on users (and administrators and SYSTEM) may
    /// connect; network logons are explicitly denied, so the pipe cannot be
    /// used from another machine.
    /// </summary>
    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            ServiceProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }
}
