using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class CollectionPromiseTests
{
    private static readonly DateOnly Today = TeamWork.Today(DateTimeOffset.UtcNow);
    private static string Path(Guid id) => $"/api/performance/{id}/collection/promise";
    private static async Task<(Guid Id, Guid Note)> Seed(WorkflowApiFactory f)
    {
        var brand = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Currency = "TRY", Name = "Söz test anlaşması" };
        var p = new MonthlyPerformance { BrandId = brand, Deal = deal, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Invoiced,
            OvoFee = 100.1234m, NetRevenue = 1000, OvoGrossProfit = 80, CommissionBreakdownJson = "{\"test\":1}",
            Collection = new() { ReceivableAmount = 100.1234m, Currency = "TRY", Revision = 1, DueOn = Today.AddDays(-10) } };
        var note = new BrandContactNote { Id = Guid.NewGuid(), BrandId = brand, ContactOn = Today.AddDays(-1), Text = "Marka yetkilisi cuma 60,1234 TL ödeme yapacağını bildirdi." };
        db.AddRange(p, note); await db.SaveChangesAsync(); return (p.Id, note.Id);
    }
    private static CollectionPromiseRequest Request(Guid note) => new(60.1234m, Today, note, WorkflowApiFactory.AccountId("partner@ovo.test"), "Görüşmedeki ödeme planı kaydedildi.", 0, 1);
    private static Task<JsonElement> Read(HttpClient c, Guid id) => c.GetFromJsonAsync<JsonElement>(Path(id));
    private static decimal Remaining(JsonElement read) => read.GetProperty("expectation").GetProperty("remaining").GetDecimal();

    [Fact]
    public async Task Promise_payment_void_cancel_and_revision_history_keep_financial_snapshot()
    {
        await using var f = new WorkflowApiFactory(); var (id, note) = await Seed(f); using var c = CustomerPortalTests.Staff(f);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Path(id), Request(note))).StatusCode);
        var read = await Read(c, id); Assert.Equal(60.1234m, Remaining(read));
        Assert.Equal(100.1234m, read.GetProperty("balance").GetProperty("outstanding").GetDecimal());
        Assert.DoesNotContain("paymentIdsAtRecording", read.ToString());
        var payment = new PaymentRequest(Guid.NewGuid(), 20.0001m, Today.AddDays(-2), "PROMISE-PAYMENT", "Sonradan girilen gerçek ödeme", 1);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/performance/{id}/collection/payments", payment)).StatusCode);
        Assert.Equal(40.1233m, Remaining(await Read(c, id)));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(id), Request(note) with { Revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/performance/{id}/collection/payments/{payment.Id}/void", new VoidPaymentRequest("Yanlış ödeme kaydı", 2))).StatusCode);
        Assert.Equal(60.1234m, Remaining(await Read(c, id)));
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Path(id), Request(note) with { Amount = 50, Revision = 1, CollectionRevision = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(Path(id) + "/cancel", new CancelPromiseRequest("Marka ödeme sözünü geri çekti.", 2, 3))).StatusCode);
        read = await Read(c, id); Assert.Equal(0, Remaining(read)); Assert.Equal("Cancelled", read.GetProperty("expectation").GetProperty("state").GetString());
        var history = await c.GetFromJsonAsync<JsonElement>(Path(id) + "/history"); Assert.Equal(3, history.GetProperty("total").GetInt32());
        Assert.Contains("Marka yetkilisi", history.GetProperty("items")[0].GetProperty("newValueJson").GetString());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.FindAsync(id);
        Assert.Equal(100.1234m, p!.OvoFee); Assert.Equal(1000, p.NetRevenue); Assert.Equal(80, p.OvoGrossProfit);
        Assert.Equal("{\"test\":1}", p.CommissionBreakdownJson); Assert.Equal(MonthlyPerformanceStatus.Invoiced, p.Status);
        Assert.Single(await db.CollectionPayments.ToListAsync()); Assert.Single(await db.CollectionPromises.ToListAsync());
    }

    [Fact]
    public async Task Revised_promise_starts_from_remaining_not_original_amount_and_task_is_never_duplicated_or_reassigned()
    {
        await using var f = new WorkflowApiFactory(); var (id, note) = await Seed(f); using var c = CustomerPortalTests.Staff(f, "Partner");
        await c.PutAsJsonAsync(Path(id), Request(note));
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Path(id) + "/task", new PromiseTaskRequest(Today, 1, 1))).StatusCode);
        var read = await Read(c, id); var taskId = read.GetProperty("task").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/work-tasks/{taskId}/completion", new WorkCompletionRequest(true, 0))).StatusCode);
        await c.PostAsJsonAsync($"/api/performance/{id}/collection/payments", new PaymentRequest(Guid.NewGuid(), 20.0001m, Today, "PROMISE-OLD", "", 1));
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Path(id), Request(note) with { Amount = 70.1233m, Revision = 2, CollectionRevision = 2, OwnerId = WorkflowApiFactory.AccountId("admin@ovo.test") })).StatusCode);
        read = await Read(c, id); Assert.Equal(70.1233m, Remaining(read)); Assert.Equal("Partner", read.GetProperty("task").GetProperty("assigneeName").GetString());
        Assert.NotEqual(JsonValueKind.Null, read.GetProperty("task").GetProperty("completedAt").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path(id) + "/task", new PromiseTaskRequest(Today, 3, 2))).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.WorkTasks.ToListAsync()); Assert.Contains("Marka yetkilisi", (await db.WorkTasks.SingleAsync()).Description);
    }

    [Fact]
    public async Task Invalid_inputs_cross_brand_notes_closed_owner_stale_revisions_and_roles_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); var (id, note) = await Seed(f); using var c = CustomerPortalTests.Staff(f);
        using var analyst = CustomerPortalTests.Staff(f, "Analyst");
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync(Path(id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync(Path(id), Request(note))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync(Path(id) + "/cancel", new CancelPromiseRequest("Neden", 1, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync(Path(id) + "/task", new PromiseTaskRequest(Today, 1, 1))).StatusCode);
        foreach (var r in new[] { Request(note) with { Amount = 0 }, Request(note) with { Amount = -1 }, Request(note) with { Amount = .00001m }, Request(note) with { Reason = " " }, Request(note) with { PromisedOn = default }, Request(note) with { ContactNoteId = Guid.Empty } })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path(id), r)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path(id), Request(note) with { PromisedOn = Today.AddDays(-2) })).StatusCode);
        foreach (var r in new[] { Request(note) with { Amount = 101 }, Request(note) with { Revision = 2 }, Request(note) with { CollectionRevision = 0 } })
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(id), r)).StatusCode);
        Guid foreignNote;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var other = new Brand { Name = "Diğer marka" };
            var n = new BrandContactNote { Id = Guid.NewGuid(), BrandId = other.Id, ContactOn = Today, Text = "Başka markanın sözü" }; foreignNote = n.Id;
            db.AddRange(other, n); (await db.UserAccounts.FindAsync(WorkflowApiFactory.AccountId("partner@ovo.test")))!.IsActive = false; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(id), Request(note))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(id), Request(foreignNote) with { OwnerId = WorkflowApiFactory.AccountId("admin@ovo.test") })).StatusCode);
        using var check = f.Services.CreateScope(); var saved = check.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Empty(await saved.CollectionPromises.ToListAsync()); Assert.Empty(await saved.WorkTasks.ToListAsync());
    }

    [Theory]
    [InlineData(MonthlyPerformanceStatus.Draft, 100)]
    [InlineData(MonthlyPerformanceStatus.Locked, 100)]
    [InlineData(MonthlyPerformanceStatus.Paid, 0)]
    public async Task Non_eligible_periods_cannot_record_promises(MonthlyPerformanceStatus status, decimal amount)
    {
        await using var f = new WorkflowApiFactory(); var (id, note) = await Seed(f); using var c = CustomerPortalTests.Staff(f);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.Include(x => x.Collection).SingleAsync(x => x.Id == id); p.Status = status; p.Collection!.ReceivableAmount = amount; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(id), Request(note))).StatusCode);
    }
}
