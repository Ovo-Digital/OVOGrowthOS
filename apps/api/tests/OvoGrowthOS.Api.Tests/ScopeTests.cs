using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class ScopeTests
{
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return c;
    }
    private static async Task<Guid> Seed(WorkflowApiFactory f, DealStatus status = DealStatus.Active)
    {
        var brand = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Name = "Kapsam denemesi", Status = status, Currency = "TRY" };
        db.Add(deal); await db.SaveChangesAsync(); return deal.Id;
    }
    private static ScopeItemRequest Item(string title, Guid? id = null) => new(id ?? Guid.NewGuid(), title, "Açıklama");
    private static ScopeCreateRequest Request(string title, Guid? id = null) => new(id ?? Guid.NewGuid(), title, "Paket dışı talep açıklaması");

    [Fact]
    public async Task Scope_items_requests_and_decisions_are_tracked_and_approval_adds_exactly_one_item()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f); using var admin = Client(f); using var analyst = Client(f, "Analyst");
        var item = Item("İçerik üretimi");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", item)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", item)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", Item("İçerik üretimi"))).StatusCode);
        var request = Request("Ek video çekimi");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/deals/{deal}/scope/requests", Request("Analist talebi"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests/{request.Id}/decision", new ScopeDecisionRequest("", "Not"))).StatusCode);

        var decided = await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests/{request.Id}/decision", new ScopeDecisionRequest("approved", "Marka ek bütçeyi onayladı"));
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        var after = await decided.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, after.GetProperty("counts").GetProperty("active").GetInt32());
        Assert.Equal(0, after.GetProperty("counts").GetProperty("pending").GetInt32());
        Assert.Equal(1, after.GetProperty("counts").GetProperty("approved").GetInt32());
        Assert.Equal("Onaylandı", after.GetProperty("requests")[0].GetProperty("statusLabel").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests/{request.Id}/decision", new ScopeDecisionRequest("rejected", "Sonra değerlendirilecek"))).StatusCode);

        var rejected = Request("Ücretsiz ek destek");
        (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests", rejected)).EnsureSuccessStatusCode();
        var rejectedRead = await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests/{rejected.Id}/decision", new ScopeDecisionRequest("rejected", "Kapsam dışında kaldı"));
        Assert.Equal(HttpStatusCode.OK, rejectedRead.StatusCode);
        var read = await analyst.GetFromJsonAsync<JsonElement>($"/api/deals/{deal}/scope");
        Assert.Equal(1, read.GetProperty("counts").GetProperty("rejected").GetInt32());
        Assert.True(read.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public async Task Scope_removal_is_reasoned_and_frozen_states_cannot_be_changed()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f); using var admin = Client(f);
        var item = Item("Kaldırılacak kapsam");
        (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", item)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items/{item.Id}/remove", new ScopeRemoveRequest(""))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items/{item.Id}/remove", new ScopeRemoveRequest("Marka kapsamı daralttı"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items/{item.Id}/remove", new ScopeRemoveRequest("Yine de çıkar"))).StatusCode);
        (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", Item("Kaldırılacak kapsam"))).EnsureSuccessStatusCode();

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var d = await db.Deals.SingleAsync(x => x.Id == deal);
            d.Status = DealStatus.Terminated; await db.SaveChangesAsync();
        }
        var frozen = await admin.GetFromJsonAsync<JsonElement>($"/api/deals/{deal}/scope");
        Assert.False(frozen.GetProperty("editable").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/items", Item("Yeni kalem"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/deals/{deal}/scope/requests", Request("Yeni talep"))).StatusCode);
    }
}
