using System.ComponentModel;
using System.Runtime.InteropServices;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.App.Services;

/// <summary>
/// Raw system proxy configuration, as stored by Windows. Used to put the
/// exact previous state back when a change is reverted.
/// </summary>
public sealed record ProxyState(int Flags, string? Server, string? Bypass, string? AutoConfigUrl);

/// <summary>
/// Reads and writes the current user's system (WinINet) proxy through the
/// documented per-connection option API, then tells running applications to
/// reload it. The Windows proxy is per user, not per adapter.
/// </summary>
public sealed class ProxyService
{
    public void Apply(ProxySettings proxy)
    {
        var state = proxy.Mode switch
        {
            ProxyMode.Off => new ProxyState(PROXY_TYPE_DIRECT, null, null, null),
            ProxyMode.Manual => new ProxyState(PROXY_TYPE_DIRECT | PROXY_TYPE_PROXY, proxy.Server.Trim(), BuildBypass(proxy), null),
            ProxyMode.AutoConfig => new ProxyState(PROXY_TYPE_DIRECT | PROXY_TYPE_AUTO_PROXY_URL, null, null, proxy.AutoConfigUrl.Trim()),
            _ => null,
        };
        if (state is not null)
            Write(state);
    }

    public ProxyState Read()
    {
        // FLAGS_UI reports the "Automatically detect settings" switch accurately;
        // older systems only understand FLAGS.
        return TryQuery(INTERNET_PER_CONN_FLAGS_UI) ?? TryQuery(INTERNET_PER_CONN_FLAGS)
            ?? throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the current proxy settings.");
    }

    public void Write(ProxyState state)
    {
        var strings = new List<IntPtr>();
        var options = new INTERNET_PER_CONN_OPTION[4];
        options[0] = new INTERNET_PER_CONN_OPTION { dwOption = INTERNET_PER_CONN_FLAGS, Value = { dwValue = state.Flags } };
        options[1] = StringOption(INTERNET_PER_CONN_PROXY_SERVER, state.Server, strings);
        options[2] = StringOption(INTERNET_PER_CONN_PROXY_BYPASS, state.Bypass, strings);
        options[3] = StringOption(INTERNET_PER_CONN_AUTOCONFIG_URL, state.AutoConfigUrl, strings);

        var optionSize = Marshal.SizeOf<INTERNET_PER_CONN_OPTION>();
        var optionsPtr = Marshal.AllocCoTaskMem(optionSize * options.Length);
        try
        {
            for (var i = 0; i < options.Length; i++)
                Marshal.StructureToPtr(options[i], optionsPtr + i * optionSize, fDeleteOld: false);

            var list = NewList(optionsPtr, options.Length);
            if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, ref list, list.dwSize))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the proxy settings.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(optionsPtr);
            foreach (var s in strings)
                Marshal.FreeHGlobal(s);
        }

        // Ask running WinINet clients to pick up the change.
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }

    private static ProxyState? TryQuery(int flagsOption)
    {
        int[] optionIds = [flagsOption, INTERNET_PER_CONN_PROXY_SERVER, INTERNET_PER_CONN_PROXY_BYPASS, INTERNET_PER_CONN_AUTOCONFIG_URL];
        var optionSize = Marshal.SizeOf<INTERNET_PER_CONN_OPTION>();
        var optionsPtr = Marshal.AllocCoTaskMem(optionSize * optionIds.Length);
        try
        {
            for (var i = 0; i < optionIds.Length; i++)
                Marshal.StructureToPtr(new INTERNET_PER_CONN_OPTION { dwOption = optionIds[i] }, optionsPtr + i * optionSize, fDeleteOld: false);

            var list = NewList(optionsPtr, optionIds.Length);
            var size = list.dwSize;
            if (!InternetQueryOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, ref list, ref size))
                return null;

            var results = new INTERNET_PER_CONN_OPTION[optionIds.Length];
            for (var i = 0; i < optionIds.Length; i++)
                results[i] = Marshal.PtrToStructure<INTERNET_PER_CONN_OPTION>(optionsPtr + i * optionSize);

            return new ProxyState(
                results[0].Value.dwValue,
                TakeString(results[1].Value.pszValue),
                TakeString(results[2].Value.pszValue),
                TakeString(results[3].Value.pszValue));
        }
        finally
        {
            Marshal.FreeCoTaskMem(optionsPtr);
        }
    }

    /// <summary>Copies a string returned by WinINet and frees its buffer.</summary>
    private static string? TakeString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        var value = Marshal.PtrToStringUni(ptr);
        GlobalFree(ptr);
        return value;
    }

    private static INTERNET_PER_CONN_OPTION_LIST NewList(IntPtr options, int count) => new()
    {
        dwSize = Marshal.SizeOf<INTERNET_PER_CONN_OPTION_LIST>(),
        pszConnection = IntPtr.Zero, // default (LAN) connection
        dwOptionCount = count,
        pOptions = options,
    };

    private static string BuildBypass(ProxySettings proxy)
    {
        var entries = proxy.BypassList
            .Split([';', ',', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !e.Equals("<local>", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (proxy.BypassLocal)
            entries.Add("<local>");
        return string.Join(';', entries);
    }

    private static INTERNET_PER_CONN_OPTION StringOption(int option, string? value, List<IntPtr> allocations)
    {
        var ptr = value is null ? IntPtr.Zero : Marshal.StringToHGlobalUni(value);
        if (ptr != IntPtr.Zero)
            allocations.Add(ptr);
        return new INTERNET_PER_CONN_OPTION { dwOption = option, Value = { pszValue = ptr } };
    }

    private const int INTERNET_OPTION_REFRESH = 37;
    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_PER_CONNECTION_OPTION = 75;

    private const int INTERNET_PER_CONN_FLAGS = 1;
    private const int INTERNET_PER_CONN_PROXY_SERVER = 2;
    private const int INTERNET_PER_CONN_PROXY_BYPASS = 3;
    private const int INTERNET_PER_CONN_AUTOCONFIG_URL = 4;
    private const int INTERNET_PER_CONN_FLAGS_UI = 10;

    private const int PROXY_TYPE_DIRECT = 0x1;
    private const int PROXY_TYPE_PROXY = 0x2;
    private const int PROXY_TYPE_AUTO_PROXY_URL = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct INTERNET_PER_CONN_OPTION_LIST
    {
        public int dwSize;
        public IntPtr pszConnection;
        public int dwOptionCount;
        public int dwOptionError;
        public IntPtr pOptions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INTERNET_PER_CONN_OPTION
    {
        public int dwOption;
        public OptionValue Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct OptionValue
    {
        [FieldOffset(0)] public int dwValue;
        [FieldOffset(0)] public IntPtr pszValue;
        [FieldOffset(0)] public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
    }

    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "InternetSetOptionW")]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, ref INTERNET_PER_CONN_OPTION_LIST lpBuffer, int dwBufferLength);

    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "InternetSetOptionW")]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "InternetQueryOptionW")]
    private static extern bool InternetQueryOption(IntPtr hInternet, int dwOption, ref INTERNET_PER_CONN_OPTION_LIST lpBuffer, ref int lpdwBufferLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
