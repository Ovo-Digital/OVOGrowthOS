using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record QualityTaskRequest(Guid BrandId, int Year, int Month, Guid? AssigneeId, DateOnly? DueOn);

public static partial class WorkflowEndpoints
{
    private static void MapDataQuality(WebApplication app)
    {
        app.MapGet("/api/data-quality", ListQuality).RequireAuthorization("ReadAccess");
        app.MapPost("/api/data-quality/track-task", CreateQualityTask).RequireAuthorization("OperationsWrite");
    }

    private static async Task<IResult> ListQuality(int? year, int? month, Guid? brandId, AppDbContext db)
    {
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var y = year ?? today.Year; var m = month ?? today.Month;
        if (y is < 2020 or > 2100 || m is < 1 or > 12) return Results.BadRequest(new { error = "Geçerli bir yıl ve ay seçin." });
        return Results.Ok(await BuildQualityReport(db, y, m, brandId));
    }

    private static async Task<QualityReport> BuildQualityReport(AppDbContext db, int year, int month, Guid? brandId)
    {
        var period = new QualityPeriod(year, month);
        var lastMonth = DataQuality.Previous(period);
        var rows = await db.MonthlyPerformances.AsNoTracking().Where(x => x.Year == year && x.Month == month).ToListAsync();
        var previousRows = await db.MonthlyPerformances.AsNoTracking().Where(x => x.Year == lastMonth.Year && x.Month == lastMonth.Month).ToListAsync();
        var deals = await db.Deals.AsNoTracking()
            .Where(x => x.Status == DealStatus.Active || x.Status == DealStatus.Expired || x.Status == DealStatus.Terminated)
            .ToListAsync();
        var rowDealIds = rows.Select(x => x.DealId).Distinct().ToList();
        var rowDeals = await db.Deals.AsNoTracking().Where(x => rowDealIds.Contains(x.Id)).ToListAsync();

        var ids = rows.Select(x => x.BrandId).Concat(deals.Select(x => x.BrandId)).Distinct().ToList();
        if (brandId.HasValue && !ids.Contains(brandId.Value) && await db.Brands.AsNoTracking().AnyAsync(x => x.Id == brandId.Value)) ids.Add(brandId.Value);
        var brandRows = await db.Brands.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var followUps = await db.BrandFollowUps.AsNoTracking().Where(x => x.OwnerId != null && ids.Contains(x.BrandId)).ToListAsync();
        var owners = followUps.GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First().OwnerId!.Value);
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(x => x.Kind == WorkKind.MonthlyClose && x.Year == year && x.Month == month && ids.Contains(x.BrandId)).ToListAsync();
        var taskByBrand = tasks.GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First());
        var accounts = await db.UserAccounts.AsNoTracking().ToListAsync();
        var byEmail = accounts.GroupBy(x => x.Email.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var byId = accounts.ToDictionary(x => x.Id, x => x);
        var vatRate = await db.GeneralSettings.AsNoTracking().Select(x => (decimal?)x.DefaultVatRate).SingleOrDefaultAsync() ?? .20m;

        var rowIds = rows.Select(x => x.Id.ToString()).ToList();
        var created = await db.AuditRecords.AsNoTracking()
            .Where(a => a.EntityType == "MonthlyPerformance" && a.Action == "MonthlyPerformanceCreated" && rowIds.Contains(a.EntityId))
            .OrderBy(a => a.CreatedAt).ToListAsync();
        var firstCreated = created.GroupBy(a => a.EntityId).ToDictionary(g => g.Key, g => g.First());
        const string importPrefix = "Dosyadan aktarım: ";
        var batchIds = firstCreated.Values.Where(a => a.Reason.StartsWith(importPrefix))
            .Select(a => a.Reason[importPrefix.Length..]).Distinct().ToList();
        var imports = await db.AuditRecords.AsNoTracking()
            .Where(a => a.Action == "MonthlyPerformanceImported" && batchIds.Contains(a.EntityId)).ToListAsync();
        var importName = imports.GroupBy(a => a.EntityId).ToDictionary(g => g.Key, g => ImportFileName(g.First().NewValueJson));

        var items = new List<BrandQuality>();
        foreach (var id in ids)
        {
            if (!brandRows.TryGetValue(id, out var brand)) continue;
            var current = rows.FirstOrDefault(x => x.BrandId == id);
            var previous = previousRows.FirstOrDefault(x => x.BrandId == id);
            var deal = PickDeal(id, current, deals, rowDeals, period);
            var audit = current is null ? null : firstCreated.GetValueOrDefault(current.Id.ToString());
            var origin = OriginOf(audit, importName);
            var owner = owners.TryGetValue(id, out var ownerId) ? ownerId : (Guid?)null;
            var responsible = Responsible(current, origin.user, owner, byEmail, byId);
            var task = taskByBrand.TryGetValue(id, out var existing) ? existing : null;
            items.Add(DataQuality.Evaluate(new QualityInput(year, month, id, brand.Name, deal, current, previous,
                origin.kind, origin.detail, responsible.name, responsible.id, task?.Id, task?.CompletedAt is not null, vatRate)));
        }
        items.Sort((a, b) => string.Compare(a.BrandName, b.BrandName, StringComparison.CurrentCultureIgnoreCase));
        return new QualityReport(period, DataQuality.Label(year, month), DataQuality.Summarize(items), items);
    }

    private static Deal? PickDeal(Guid brandId, MonthlyPerformance? current, List<Deal> deals, List<Deal> rowDeals, QualityPeriod period)
    {
        if (current is not null && rowDeals.FirstOrDefault(x => x.Id == current.DealId) is { } owned) return owned;
        var brandDeals = deals.Where(x => x.BrandId == brandId).ToList();
        var reportPeriod = new ReportPeriod(period.Year, period.Month);
        return brandDeals.FirstOrDefault(x => PortfolioReporting.ExpectedInPeriod(x, reportPeriod)) ?? brandDeals.FirstOrDefault();
    }

    private static (string kind, string detail, string user) OriginOf(AuditRecord? created, Dictionary<string, string> importName)
    {
        if (created is null) return ("none", "", "");
        const string prefix = "Dosyadan aktarım: ";
        if (created.Reason.StartsWith(prefix))
        {
            var batch = created.Reason[prefix.Length..];
            return ("import", importName.GetValueOrDefault(batch) ?? "", created.UserId);
        }
        return ("manual", created.UserId, created.UserId);
    }

    private static string ImportFileName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty("fileName", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() ?? "" : "";
        }
        catch (JsonException) { return ""; }
    }

    private static (Guid? id, string name) Responsible(MonthlyPerformance? current, string originUser, Guid? owner,
        Dictionary<string, UserAccount> byEmail, Dictionary<Guid, UserAccount> byId)
    {
        if (current is not null && current.PreparedBy.Length > 0) return Resolve(current.PreparedBy, byEmail);
        if (originUser.Length > 0) return Resolve(originUser, byEmail);
        if (owner.HasValue && byId.TryGetValue(owner.Value, out var account)) return (account.Id, account.Name);
        return (null, "");
    }

    private static (Guid? id, string name) Resolve(string email, Dictionary<string, UserAccount> byEmail) =>
        byEmail.TryGetValue(email.ToLowerInvariant(), out var account) ? (account.Id, account.Name) : (null, email);

    private static async Task<IResult> CreateQualityTask(QualityTaskRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (r.Year is < 2020 or > 2100 || r.Month is < 1 or > 12) return Results.BadRequest(new { error = "Geçerli bir yıl ve ay seçin." });
        if (r.DueOn is { } due && due.Year is < 2020 or > 2100) return Results.BadRequest(new { error = "Geçerli bir son tarih seçin." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == r.BrandId)) return Results.NotFound();
        var report = await BuildQualityReport(db, r.Year, r.Month, r.BrandId);
        var item = report.Brands.SingleOrDefault();
        if (item is null || item.Readiness is "ready" or "notApplicable")
            return Results.Conflict(new { error = "Bu marka için izlenecek eksik kaynak bulunmuyor. Yalnız kaydı olmayan veya inceleme bekleyen markalar için takip işi açılır." });
        if (item.TaskId is not null)
            return Results.Conflict(new { error = "Bu markanın aynı dönem için kapanış görevi zaten var. Mevcut görevi düzenleyin." });
        if (item.DealId is null) return Results.Conflict(new { error = "Takip işi için bu markanın anlaşması gereklidir." });
        var assignee = r.AssigneeId ?? item.ResponsibleId ?? Guid.Parse(user.FindFirstValue("uid")!);
        var request = new WorkTaskRequest(Guid.NewGuid(), r.BrandId, assignee,
            $"{r.Month}/{r.Year} eksik kaynak takibi", TaskDescription(item, report.Period),
            WorkPriority.High, WorkKind.MonthlyClose, item.DealId, r.Year, r.Month, r.DueOn ?? NextFifth(r.Year, r.Month));
        return await CreateWorkTask(request, db, user);
    }

    private static DateOnly NextFifth(int year, int month)
    {
        var next = month == 12 ? new DateOnly(year + 1, 1, 1) : new DateOnly(year, month + 1, 1);
        return new DateOnly(next.Year, next.Month, 5);
    }

    private static string TaskDescription(BrandQuality item, QualityPeriod period)
    {
        var lines = new List<string> { $"{DataQuality.Label(period.Year, period.Month)} dönemi veri kalitesi bulguları:" };
        foreach (var source in item.Sources.Where(x => x.State is "missing" or "zero").Take(4)) lines.Add($"{source.Label}: {source.Note}");
        foreach (var alert in item.Alerts.Take(8)) lines.Add($"{alert.Finding} {alert.NextStep}");
        lines.Add($"Ayrıntı: /data-quality?year={period.Year}&month={period.Month}");
        lines.Add("Bu görev yalnız takip içindir; dönemi onaylamaz, kilitlemez ve sonucu değiştirmez.");
        var text = string.Join("\n", lines);
        return text.Length <= 4000 ? text : text[..4000];
    }
}
