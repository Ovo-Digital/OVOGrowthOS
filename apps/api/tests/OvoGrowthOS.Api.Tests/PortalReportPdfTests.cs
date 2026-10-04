using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortalReportPdfTests
{
    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}";

    private static async Task<Guid> Publish(HttpClient admin, Guid brand, Guid period)
    {
        var response = await admin.PostAsJsonAsync(Root(brand) + "/reports", new PortalPublishRequest(period));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public void Build_produces_a_pdf_document_from_a_snapshot()
    {
        var snapshot = new PortalReportSnapshot("Lale", "TRY", 1, DateTimeOffset.UtcNow,
            new BrandReportMetrics(Guid.NewGuid(), 2026, 8, MonthlyPerformanceStatus.Locked, 1000, 100, 50, 20m, 0.01m, 500, 0, 500),
            [new ReportExplanation("Net ciro yükseldi.", "Gelir artışı olduğu görülüyor.", "Bütçeyi gözden geçirin.")]);
        var bytes = PortalReportPdf.Build(snapshot, 2026, 8);
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Published_report_downloads_as_a_pdf_for_both_the_customer_and_the_staff()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        var report = await Publish(admin, s.BrandId, s.PeriodId);

        var portal = await c.GetAsync($"/api/portal/reports/{report}/pdf");
        portal.EnsureSuccessStatusCode();
        var bytes = await portal.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("application/pdf", portal.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".pdf", portal.Content.Headers.ContentDisposition?.FileName);

        var staff = await admin.GetAsync($"{Root(s.BrandId)}/reports/{report}/pdf");
        staff.EnsureSuccessStatusCode();
        var staffBytes = await staff.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(staffBytes, 0, 4));

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/portal/reports/{Guid.NewGuid()}/pdf")).StatusCode);
        (await admin.PostAsync($"{Root(s.BrandId)}/reports/{report}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/portal/reports/{report}/pdf")).StatusCode);
    }

    [Fact]
    public async Task Analyst_cannot_download_the_report_pdf()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var analyst = CustomerPortalTests.Staff(f, "Analyst");
        var report = await Publish(admin, s.BrandId, s.PeriodId);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync($"{Root(s.BrandId)}/reports/{report}/pdf")).StatusCode);
    }

    [Fact]
    public async Task Pdf_attachment_is_off_by_default_and_is_audited_when_enabled()
    {
        await using var parent = new WorkflowApiFactory();
        var s = await CustomerPortalTests.Seed(parent);
        using var admin = CustomerPortalTests.Staff(parent);
        var brand = s.BrandId;
        var initial = await admin.GetFromJsonAsync<JsonElement>(Root(brand) + "/email-policy");
        Assert.False(initial.GetProperty("pdfAttachmentEnabled").GetBoolean());

        var enabled = await admin.PutAsJsonAsync(Root(brand) + "/email-policy",
            new BrandMailPolicyRequest(true, "{marka} · {donem}", "Merhaba {marka}: {donem}\n{baglanti}", "PDF eki kararı", 0, false, 5, 9, true));
        enabled.EnsureSuccessStatusCode();
        var saved = await admin.GetFromJsonAsync<JsonElement>(Root(brand) + "/email-policy");
        Assert.True(saved.GetProperty("pdfAttachmentEnabled").GetBoolean());

        await using var scope = parent.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditRecords.SingleAsync(x => x.Action == "BrandMailPolicyChanged");
        Assert.Contains("pdfAttachmentEnabled", audit.NewValueJson, StringComparison.OrdinalIgnoreCase);

        var disabled = await admin.PutAsJsonAsync(Root(brand) + "/email-policy",
            new BrandMailPolicyRequest(false, "{marka} · {donem}", "Merhaba {marka}: {donem}\n{baglanti}", "Rapor e-postası kapatıldı", 1, false, 5, 9, true));
        disabled.EnsureSuccessStatusCode();
        var after = await admin.GetFromJsonAsync<JsonElement>(Root(brand) + "/email-policy");
        Assert.False(after.GetProperty("pdfAttachmentEnabled").GetBoolean());
    }
}
