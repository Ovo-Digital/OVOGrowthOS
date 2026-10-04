using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class SatisfactionApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    [Fact]
    public async Task Ratings_are_saved_per_period_recalculated_and_audited_without_touching_health_score()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = await Client(f);

        var created = await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction",
            new SatisfactionRatingRequest(2026, 9, 5, "Geri bildirimlerine hızlı dönüldü."));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction", new SatisfactionRatingRequest(2026, 8, 3, ""));
        await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction", new SatisfactionRatingRequest(2026, 7, 4, null));

        using var read = JsonDocument.Parse(await admin.GetStringAsync($"/api/brands/{brand}/satisfaction"));
        var root = read.RootElement;
        Assert.Equal(4.0m, root.GetProperty("average").GetDecimal());
        Assert.Equal(3, root.GetProperty("count").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { 2026, 2026, 2026 }, items.Select(x => x.GetProperty("year").GetInt32()).ToArray());
        Assert.Equal(new[] { 9, 8, 7 }, items.Select(x => x.GetProperty("month").GetInt32()).ToArray());
        Assert.Equal("Çok iyi", items[0].GetProperty("scoreLabel").GetString());
        Assert.Equal("Orta", items[1].GetProperty("scoreLabel").GetString());
        Assert.Equal("", items[1].GetProperty("comment").GetString());
        Assert.Equal("admin@ovo.test", items[0].GetProperty("createdBy").GetString());

        var revised = await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction", new SatisfactionRatingRequest(2026, 9, 2, "Yeni dönem."));
        revised.EnsureSuccessStatusCode();
        using var updated = JsonDocument.Parse(await revised.Content.ReadAsStringAsync());
        Assert.Equal(3, updated.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(3.0m, updated.RootElement.GetProperty("average").GetDecimal());
        Assert.Equal("Kötü", updated.RootElement.GetProperty("items")[0].GetProperty("scoreLabel").GetString());

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.SatisfactionRatings.CountAsync(x => x.BrandId == brand));
        var audit = await db.AuditRecords.Where(x => x.Action.StartsWith("SatisfactionRating")).ToListAsync();
        Assert.Equal(4, audit.Count);
        Assert.Contains(audit, x => x.Action == "SatisfactionRatingCreated");
        Assert.Contains(audit, x => x.Action == "SatisfactionRatingChanged");
        var period = await db.MonthlyPerformances.Where(x => x.BrandId == brand).ToListAsync();
        Assert.Empty(period);
    }

    [Fact]
    public async Task Invalid_values_return_turkish_errors_and_unknown_brand_is_not_found()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = await Client(f);

        var invalid = new (SatisfactionRatingRequest Request, string Message)[]
        {
            (new(2026, 9, 0, null), "Puan 1 ile 5 arasında bir sayı olmalıdır."),
            (new(2026, 9, 6, null), "Puan 1 ile 5 arasında bir sayı olmalıdır."),
            (new(2026, 13, 4, null), "Geçerli bir yıl ve ay seçin."),
            (new(2019, 1, 4, null), "Geçerli bir yıl ve ay seçin."),
            (new(2026, 9, 4, new string('x', 1001)), "Yorum en fazla 1000 karakter olabilir.")
        };
        foreach (var (bad, message) in invalid)
        {
            var response = await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction", bad);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(message, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/brands/{Guid.NewGuid()}/satisfaction",
            new SatisfactionRatingRequest(2026, 9, 4, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/brands/{Guid.NewGuid()}/satisfaction")).StatusCode);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.SatisfactionRatings.CountAsync());
    }

    [Fact]
    public async Task Reading_needs_read_access_but_saving_needs_operations_write()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var analyst = await Client(f, "analyst@ovo.test");
        using var admin = await Client(f);

        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/brands/{brand}/satisfaction",
            new SatisfactionRatingRequest(2026, 9, 4, null))).StatusCode);
        (await analyst.GetAsync($"/api/brands/{brand}/satisfaction")).EnsureSuccessStatusCode();

        await admin.PostAsJsonAsync($"/api/brands/{brand}/satisfaction", new SatisfactionRatingRequest(2026, 9, 4, "Analist okudu."));
        using var read = JsonDocument.Parse(await admin.GetStringAsync($"/api/brands/{brand}/satisfaction"));
        Assert.Equal(1, read.RootElement.GetProperty("count").GetInt32());
    }
}
