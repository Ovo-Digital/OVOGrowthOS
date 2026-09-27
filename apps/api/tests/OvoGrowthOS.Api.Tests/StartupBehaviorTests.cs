using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Tests;

public sealed class StartupBehaviorTests
{
    [Fact]
    public async Task Readiness_endpoint_reports_ready_database()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Exhausted_login_limit_does_not_block_session_or_admin_actions()
    {
        await using var factory = new WorkflowApiFactory();
        using var anonymous = factory.CreateClient();
        for (var i = 0; i < 10; i++)
            await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = "Wrong-password" });
        var limited = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), limited.Headers.RetryAfter?.Delta);

        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/notifications/refresh", null)).StatusCode);
        var settings = new MailSettingsRequest(false, "smtp.gmail.com", 465, true, "", "", "OVO Digital", null, false, 0);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/account-mail/settings", settings)).StatusCode);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 10)]
    public async Task Demo_portfolio_seeds_only_when_flag_is_on(bool includeDemoData, int expectedBrands)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"seed-{Guid.NewGuid()}").Options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultAdmin:Email"] = "admin@ovo.test",
            ["DefaultAdmin:PasswordHash"] = JwtTokenService.HashPassword(WorkflowApiFactory.TestPassword)
        }).Build();
        await SeedData.InitializeAsync(db, configuration, includeDemoData);
        Assert.Equal(expectedBrands, await db.Brands.CountAsync());
        Assert.Equal("admin@ovo.test", await db.UserAccounts.Where(x => x.Role == "Admin").Select(x => x.Email).SingleAsync());
        Assert.NotEmpty(await db.RuleSets.ToListAsync());
    }
}
