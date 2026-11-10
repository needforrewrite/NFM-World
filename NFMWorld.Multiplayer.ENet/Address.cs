using System.Net;

namespace ENet;

/// <summary>
/// Managed replacement for <c>ENetAddress</c>. Holds a resolved address; IPv4 and IPv6 are both
/// supported, matching the fork's dual-stack socket behaviour.
/// </summary>
public struct Address
{
    private IPAddress? _address;

    public Address()
    {
        _address = null;
        Port = 0;
    }

    internal Address(IPAddress address, ushort port)
    {
        _address = address;
        Port = port;
    }

    public ushort Port { get; set; }

    internal readonly IPAddress? ResolvedAddress => _address;

    internal readonly IPAddress AddressOrAny =>
        _address ?? IPAddress.IPv6Any;

    public readonly string GetIP()
    {
        return (_address ?? IPAddress.Any).ToString();
    }

    public bool SetIP(string ip)
    {
        ArgumentNullException.ThrowIfNull(ip);

        if (!IPAddress.TryParse(ip, out var address))
            return false;

        _address = address;
        return true;
    }

    public readonly string GetHost() => GetIP();

    public bool SetHost(string hostName)
    {
        ArgumentNullException.ThrowIfNull(hostName);

        if (IPAddress.TryParse(hostName, out var literal))
        {
            _address = literal;
            return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(hostName);

            // Prefer IPv4; the port does not carry a scope for either family here.
            foreach (var candidate in addresses)
                if (candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    _address = candidate;
                    return true;
                }

            if (addresses.Length > 0)
            {
                _address = addresses[0];
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    internal readonly IPEndPoint ToEndPoint()
    {
        return new IPEndPoint(_address ?? IPAddress.Any, Port);
    }

    public override readonly string ToString() => $"{GetIP()}:{Port}";
}
