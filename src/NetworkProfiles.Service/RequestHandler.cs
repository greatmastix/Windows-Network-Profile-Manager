using System.Net.NetworkInformation;
using NetworkProfiles.Core.Execution;
using NetworkProfiles.Core.Ipc;
using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Validation;

namespace NetworkProfiles.Service;

/// <summary>
/// Carries out one request. Requests only ever describe settings; the
/// commands are built here from parsed values, so a caller cannot make the
/// service run anything other than the netsh address and DNS commands.
/// </summary>
public sealed class RequestHandler(NetworkConfigurator configurator, ILogger<RequestHandler> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ServiceResponse> HandleAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind == ServiceRequestKind.Ping)
            return Ok("Service is running.");

        if (request.Profile is not { } profile)
            return Fail("The request has no settings.");

        if (!AdapterExists(request.InterfaceIndex))
            return Fail($"There is no network adapter with interface index {request.InterfaceIndex}.");

        // The proxy is per user and set by the app; the service never touches it.
        profile.Proxy = new ProxySettings { Mode = ProxyMode.Keep };

        if (request.Kind == ServiceRequestKind.Apply)
        {
            var errors = ProfileValidator.Validate(profile);
            if (errors.Count > 0)
                return Fail(string.Join(Environment.NewLine, errors));
        }
        else if (request.Kind != ServiceRequestKind.Restore)
        {
            return Fail($"Unknown request {request.Kind}.");
        }

        // One change at a time, so two requests never interleave their netsh calls.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var error = await configurator.ApplyAsync(profile, request.InterfaceIndex, cancellationToken);
            if (error is not null)
            {
                logger.LogWarning("{Kind} of \"{Profile}\" on interface {Index} failed: {Error}",
                    request.Kind, profile.Name, request.InterfaceIndex, error);
                return Fail(error);
            }

            logger.LogInformation("{Kind} of \"{Profile}\" on interface {Index} succeeded.",
                request.Kind, profile.Name, request.InterfaceIndex);
            return Ok("Done.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool AdapterExists(int interfaceIndex)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (nic.Supports(NetworkInterfaceComponent.IPv4) && nic.GetIPProperties().GetIPv4Properties()?.Index == interfaceIndex)
                    return true;
            }
            catch (NetworkInformationException)
            {
                // Adapter disappeared while enumerating; ignore it.
            }
        }
        return false;
    }

    private static ServiceResponse Ok(string message) => new() { Succeeded = true, Message = message };
    private static ServiceResponse Fail(string message) => new() { Succeeded = false, Message = message };
}
