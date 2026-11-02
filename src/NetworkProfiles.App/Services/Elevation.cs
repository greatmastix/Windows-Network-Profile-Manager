using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using NetworkProfiles.Core.Ipc;

namespace NetworkProfiles.App.Services;

/// <summary>
/// The app normally runs as the signed-in user and lets the service change adapter settings.
/// Without the service (the exe run on its own, not installed), it restarts itself as administrator.
/// </summary>
public static class Elevation
{
    /// <summary>Whether the Network Profiles service is installed (it may be stopped).</summary>
    public static bool IsServiceInstalled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceProtocol.ServiceName}");
                return key is not null;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Starts this exe again as administrator with the same arguments (UAC prompt).
    /// Returns false when the user declines or it can't be started; the caller then keeps running as is.
    /// </summary>
    public static bool TryRestartElevated(IEnumerable<string> args)
    {
        if (Environment.ProcessPath is not { } exe)
            return false;
        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
        try
        {
            Process.Start(startInfo)?.Dispose();
            return true;
        }
        catch (Win32Exception)   // 1223: the user chose No in the UAC prompt
        {
            return false;
        }
    }
}
