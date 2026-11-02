using System.Net;

namespace NetworkProfiles.Core.Validation;

/// <summary>
/// Small helpers for IPv4 address and subnet mask handling.
/// </summary>
public static class Ipv4
{
    /// <summary>
    /// Parses a strict dotted-quad IPv4 address. Rejects shorthand ("10.1") and
    /// leading zeros ("010.0.0.1"), which Windows would read as octal.
    /// </summary>
    public static bool TryParseAddress(string? text, out IPAddress address)
    {
        address = IPAddress.None;
        var parts = text?.Trim().Split('.');
        if (parts is not { Length: 4 })
            return false;

        var bytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            var part = parts[i];
            if (part.Length is 0 or > 3 || !part.All(char.IsAsciiDigit) || (part.Length > 1 && part[0] == '0'))
                return false;
            var value = int.Parse(part);
            if (value > 255)
                return false;
            bytes[i] = (byte)value;
        }
        address = new IPAddress(bytes);
        return true;
    }

    /// <summary>
    /// Parses a subnet mask given either as dotted quad or as a prefix length
    /// ("24" or "/24"). Only contiguous masks are accepted.
    /// </summary>
    public static bool TryParseMask(string? text, out IPAddress mask)
    {
        mask = IPAddress.None;
        text = text?.Trim().TrimStart('/');
        if (string.IsNullOrEmpty(text))
            return false;

        if (int.TryParse(text, out var prefix))
        {
            if (prefix is < 1 or > 32)
                return false;
            mask = FromPrefix(prefix);
            return true;
        }

        if (!TryParseAddress(text, out var parsed))
            return false;
        var bits = ToUInt32(parsed);
        // A contiguous mask is a run of ones followed by zeros: inverting it
        // and adding one must give a power of two.
        var inverted = ~bits;
        if (bits == 0 || (inverted & (inverted + 1)) != 0)
            return false;
        mask = parsed;
        return true;
    }

    public static IPAddress FromPrefix(int prefix)
    {
        var bits = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return FromUInt32(bits);
    }

    public static int ToPrefix(IPAddress mask) =>
        System.Numerics.BitOperations.PopCount(ToUInt32(mask));

    public static bool SameSubnet(IPAddress a, IPAddress b, IPAddress mask)
    {
        var m = ToUInt32(mask);
        return (ToUInt32(a) & m) == (ToUInt32(b) & m);
    }

    /// <summary>True when the address is the network or broadcast address of its subnet.</summary>
    public static bool IsNetworkOrBroadcast(IPAddress address, IPAddress mask)
    {
        var m = ToUInt32(mask);
        if (m >= 0xFFFFFFFEu)
            return false; // /31 and /32 have no network or broadcast address
        var host = ToUInt32(address) & ~m;
        return host == 0 || host == ~m;
    }

    public static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    public static IPAddress FromUInt32(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}
