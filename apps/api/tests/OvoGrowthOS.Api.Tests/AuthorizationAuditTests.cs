using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;

namespace OvoGrowthOS.Api.Tests;

public sealed class AuthorizationAuditTests
{
    private static readonly string[] PublicPrefixes =
    [
        "HTTP: GET /health",
        "HTTP: GET /health/ready",
        "HTTP: POST /api/auth/complete-account",
        "HTTP: POST /api/auth/forgot-password",
        "HTTP: POST /api/auth/login",
        "HTTP: POST /api/auth/second-factor",
    ];

    [Fact]
    public async Task Every_route_is_policy_gated_or_explicitly_public()
    {
        await using var factory = new WorkflowApiFactory();
        _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var property = dataSource.GetType().GetProperty("Endpoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var endpoints = property?.GetValue(dataSource) as IEnumerable<Endpoint> ?? [];
        var unguarded = endpoints
            .Where(x => x.Metadata.GetMetadata<IAuthorizeData>() is null && x.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(x => x.DisplayName ?? "?")
            .OrderBy(x => x)
            .ToList();
        Assert.True(unguarded.Count == 0, $"Yetki başlığı olmadan açık kalan uç:\n{string.Join("\n", unguarded)}");
    }

    [Fact]
    public async Task Anonymous_routes_are_only_the_known_public_endpoints()
    {
        await using var factory = new WorkflowApiFactory();
        _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var property = dataSource.GetType().GetProperty("Endpoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var endpoints = property?.GetValue(dataSource) as IEnumerable<Endpoint> ?? [];
        var anonymous = endpoints
            .Where(x => x.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(x => x.DisplayName ?? "?")
            .OrderBy(x => x)
            .ToList();
        var unexpected = anonymous.Where(x => !PublicPrefixes.Any(prefix => x.StartsWith(prefix))).ToList();
        Assert.True(unexpected.Count == 0, $"Beklenmeyen açık uç:\n{string.Join("\n", unexpected)}");
        Assert.Equal(PublicPrefixes.Length, anonymous.Count);
    }

    [Fact]
    public async Task Admin_only_routes_reject_a_token_whose_account_changed_in_the_database()
    {
        await using var factory = new WorkflowApiFactory(); await factory.SeedAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.UserAccounts.SingleAsync(x => x.Email == "admin@ovo.test");
            admin.Role = "Analyst";
            await db.SaveChangesAsync();
        }
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.UserAccounts.SingleAsync(x => x.Email == "admin@ovo.test");
            admin.Role = "Admin";
            admin.IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }
}
