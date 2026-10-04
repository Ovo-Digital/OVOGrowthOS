using System.Net;

namespace OvoGrowthOS.Api.Http;

public static class ClientIp
{
    public static bool IsTrustedProxy(IPAddress? peer)
    {
        if (peer is null) return false;
        if (peer.IsIPv4MappedToIPv6) peer = peer.MapToIPv4();
        var octets = peer.GetAddressBytes();
        if (octets.Length != 4) return false;
        return octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31 || octets[0] == 192 && octets[1] == 168;
    }

    public static string From(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (IsTrustedProxy(peer) && context.Request.Headers.TryGetValue("X-Forwarded-For", out var values))
        {
            var entries = values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (entries.Length > 0) return entries[^1];
        }
        return peer?.ToString() ?? "unknown";
    }
}
