using System.Net;
using Microsoft.AspNetCore.Http;
using OvoGrowthOS.Api.Http;

namespace OvoGrowthOS.Api.Tests;

public sealed class ClientIpTests
{
    private static HttpContext Context(IPAddress? peer, string? forwarded = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = peer;
        if (forwarded is not null) context.Request.Headers["X-Forwarded-For"] = forwarded;
        return context;
    }

    [Fact]
    public void Missing_peer_is_reported_as_unknown_instead_of_failing()
    {
        Assert.Equal("unknown", ClientIp.From(Context(null)));
    }

    [Fact]
    public void Peer_address_wins_when_no_proxy_is_involved()
    {
        Assert.Equal("203.0.113.5", ClientIp.From(Context(IPAddress.Parse("203.0.113.5"))));
        Assert.Equal("203.0.113.5", ClientIp.From(Context(IPAddress.Parse("203.0.113.5"), "198.51.100.7")));
    }

    [Fact]
    public void Only_a_trusted_private_proxy_can_supply_the_client_address()
    {
        foreach (var proxy in new[] { "192.168.1.10", "172.16.0.9", "172.31.255.1", "::ffff:192.168.1.10" })
            Assert.Equal("198.51.100.7", ClientIp.From(Context(IPAddress.Parse(proxy), "9.9.9.9, 198.51.100.7")));

        foreach (var untrusted in new[] { "172.32.0.1", "10.0.0.5", "8.8.8.8" })
            Assert.Equal(untrusted, ClientIp.From(Context(IPAddress.Parse(untrusted), "9.9.9.9, 198.51.100.7")));
    }
}
