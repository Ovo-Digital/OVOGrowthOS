using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record WorkTaskRequest(Guid Id, Guid BrandId, Guid AssigneeId, string Title, string Description,
    WorkPriority Priority, WorkKind Kind, Guid? DealId, int? Year, int? Month, DateOnly DueOn, int Revision = 0);
public sealed record WorkCompletionRequest(bool Completed, int Revision);
public sealed record FollowUpRequest(Guid? OwnerId, LeadStage Stage, string WaitingReason, DateOnly? NextContactOn, string NextStep, int Revision);
public sealed record ContactNoteRequest(Guid Id, DateOnly ContactOn, string Text);

public static partial class WorkflowEndpoints
{
    private sealed record Reminder(string Kind, string Title, string Detail, string Href, string Priority);
    private static async Task<IResult> TeamReminders(AppDbContext db, ClaimsPrincipal user)
    {
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        var assigned = await db.WorkTasks.AsNoTracking().Include(x => x.Brand).Where(x => x.AssigneeId == actor && x.CompletedAt == null)
            .OrderBy(x => x.DueOn).ThenByDescending(x => x.Priority).Take(10).ToListAsync();
        var reminders = assigned.Select(x => new Reminder("Görev", $"{x.Brand!.Name}: {x.Title}",
            $"Son tarih: {x.DueOn:dd.MM.yyyy} · {(TeamWork.IsOverdue(x, today) ? "Gecikti" : x.DueOn == today ? "Bugün" : "Planlandı")}",
            $"/brands/{x.BrandId}#team-work", TeamWork.IsOverdue(x, today) || x.Priority == WorkPriority.High ? "Yüksek" : "Normal")).ToList();
        var contacts = await (from follow in db.BrandFollowUps.AsNoTracking()
            join brand in db.Brands on follow.BrandId equals brand.Id
            where follow.OwnerId == actor && follow.NextContactOn != null && follow.NextContactOn <= today
            orderby follow.NextContactOn
            select new { brand.Id, brand.Name, follow.NextContactOn, follow.NextStep }).Take(10).ToListAsync();
        reminders.AddRange(contacts.Select(x => new Reminder("Görüşme takibi", $"{x.Name}: takip zamanı geldi", x.NextStep,
            $"/brands/{x.Id}#team-work", "Yüksek")));
        var reviews = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Where(x => x.Status == MonthlyPerformanceStatus.UnderReview)
            .OrderBy(x => x.Year).ThenBy(x => x.Month).Take(5).ToListAsync();
        reminders.AddRange(reviews.Select(x => new Reminder("Aylık sonuç", $"{x.Brand!.Name}: {x.Month}/{x.Year} onay bekliyor",
            "Hazırlayan dışında yetkili bir kişi kontrol etmelidir.", $"/performance/{x.Id}", "Normal")));
        var evaluations = await db.Evaluations.AsNoTracking().Include(x => x.Brand)
            .Where(x => x.Status == EvaluationStatus.Draft || x.Status == EvaluationStatus.InProgress || x.Status == EvaluationStatus.ReadyForAnalysis)
            .OrderBy(x => x.UpdatedAt).Take(3).ToListAsync();
        reminders.AddRange(evaluations.Select(x => new Reminder("Değerlendirme", $"{x.Brand!.Name} değerlendirmesi bekliyor", "Kaldığınız adımdan devam edin.", $"/evaluations/{x.Id}", "Normal")));
        var accepted = await db.Deals.AsNoTracking().Include(x => x.Brand).Where(x => x.Status == DealStatus.Accepted).OrderBy(x => x.UpdatedAt).Take(2).ToListAsync();
        reminders.AddRange(accepted.Select(x => new Reminder("Anlaşma", $"{x.Brand!.Name} anlaşması etkinleştirilmeyi bekliyor", x.Name, $"/deals/{x.Id}", "Normal")));
        return Results.Ok(reminders);
    }

    private static void MapTeamWork(WebApplication app)
    {
        app.MapGet("/api/team", async (AppDbContext db) => Results.Ok(await db.UserAccounts.AsNoTracking()
            .Where(x => x.Role != "BrandClient").OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync())).RequireAuthorization("ReadAccess");
        app.MapGet("/api/work-tasks", ListWorkTasks).RequireAuthorization("ReadAccess");
        app.MapGet("/api/work-approvals", async (AppDbContext db, int page = 1) => Results.Ok(await Page(
            db.MonthlyPerformances.AsNoTracking().Where(x => x.Status == MonthlyPerformanceStatus.UnderReview)
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.BrandId, brandName = x.Brand!.Name, x.Year, x.Month, x.PreparedBy }), page, 20))).RequireAuthorization("ReadAccess");
        app.MapPost("/api/work-tasks", CreateWorkTask).AddEndpointFilter<ValidationFilter<WorkTaskRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPut("/api/work-tasks/{id:guid}", UpdateWorkTask).AddEndpointFilter<ValidationFilter<WorkTaskRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPut("/api/work-tasks/{id:guid}/completion", CompleteWorkTask).RequireAuthorization("ReadAccess");

        app.MapGet("/api/brands/{id:guid}/follow-up", async (Guid id, AppDbContext db) =>
        {
            if (!await db.Brands.AnyAsync(x => x.Id == id)) return Results.NotFound();
            var followUp = await db.BrandFollowUps.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id);
            var lastContactOn = await db.BrandContactNotes.Where(x => x.BrandId == id).MaxAsync(x => (DateOnly?)x.ContactOn);
            return Results.Ok(new { followUp = followUp ?? new BrandFollowUp { BrandId = id }, lastContactOn });
        }).RequireAuthorization("ReadAccess");
        app.MapPut("/api/brands/{id:guid}/follow-up", SaveFollowUp).AddEndpointFilter<ValidationFilter<FollowUpRequest>>().RequireAuthorization("OperationsWrite");
        app.MapGet("/api/brands/{id:guid}/contact-notes", async (Guid id, AppDbContext db, int page = 1) => Results.Ok(await Page(
            db.BrandContactNotes.AsNoTracking().Where(x => x.BrandId == id).OrderByDescending(x => x.ContactOn).ThenByDescending(x => x.CreatedAt), page, 20))).RequireAuthorization("ReadAccess");
        app.MapPost("/api/brands/{id:guid}/contact-notes", AddContactNote).AddEndpointFilter<ValidationFilter<ContactNoteRequest>>().RequireAuthorization("OperationsWrite");
        app.MapGet("/api/lead-follow-ups", async (AppDbContext db, int page = 1, int pageSize = 20) => Results.Ok(await Page(
            from brand in db.Brands.AsNoTracking()
            where brand.Status == BrandStatus.Lead || brand.Status == BrandStatus.Evaluation || brand.Status == BrandStatus.Negotiation
            join follow in db.BrandFollowUps on brand.Id equals follow.BrandId into follows
            from follow in follows.DefaultIfEmpty()
            orderby brand.UpdatedAt descending, brand.Id
            select new { brand.Id, brand.Name, brand.Status, brand.ContactName, brand.ContactEmail,
                followUp = follow, lastContactOn = db.BrandContactNotes.Where(x => x.BrandId == brand.Id).Max(x => (DateOnly?)x.ContactOn) }, page, pageSize))).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ListWorkTasks(AppDbContext db, ClaimsPrincipal user, Guid? brandId = null,
        string view = "mine", int page = 1, int pageSize = 20)
    {
        if (!new[] { "mine", "open", "overdue", "completed" }.Contains(view)) return Results.BadRequest(new { error = "Geçerli bir görev görünümü seçin." });
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var q = db.WorkTasks.AsNoTracking().AsQueryable();
        if (brandId.HasValue) q = q.Where(x => x.BrandId == brandId);
        q = view == "completed" ? q.Where(x => x.CompletedAt != null) : q.Where(x => x.CompletedAt == null);
        if (view == "mine") { var actor = Guid.Parse(user.FindFirstValue("uid")!); q = q.Where(x => x.AssigneeId == actor); }
        if (view == "overdue") q = q.Where(x => x.DueOn < today);
        return Results.Ok(await Page(q.OrderBy(x => x.DueOn).ThenByDescending(x => x.Priority).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.BrandId, brandName = x.Brand!.Name, x.AssigneeId, assigneeName = x.Assignee!.Name,
                assigneeActive = x.Assignee!.IsActive, x.Title, x.Description, x.Priority, x.Kind, x.DealId, x.Year, x.Month,
                x.DueOn, x.CompletedAt, x.CompletedBy, x.CreatedBy, x.CreatedAt, x.Revision, overdue = x.CompletedAt == null && x.DueOn < today }), page, pageSize));
    }

    private static async Task<string?> TaskTargetError(WorkTaskRequest r, AppDbContext db)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == r.BrandId)) return "Marka bulunamadı.";
        if (!await db.UserAccounts.AnyAsync(x => x.Id == r.AssigneeId && x.IsActive && x.Role != "BrandClient")) return "Görevi etkin bir çalışana atayın.";
        if (r.DealId.HasValue && !await db.Deals.AnyAsync(x => x.Id == r.DealId && x.BrandId == r.BrandId)) return "Anlaşma bu markaya ait değil.";
        if (r.Kind == WorkKind.MonthlyClose && await db.WorkTasks.AnyAsync(x => x.Id != r.Id && x.Kind == r.Kind && x.BrandId == r.BrandId && x.Year == r.Year && x.Month == r.Month)) return "Bu markanın aynı dönem için kapanış görevi zaten var. Mevcut görevi düzenleyin.";
        if (r.Kind == WorkKind.ContractRenewal && await db.WorkTasks.AnyAsync(x => x.Id != r.Id && x.Kind == r.Kind && x.DealId == r.DealId)) return "Bu anlaşmanın yenileme görevi zaten var. Mevcut görevi düzenleyin.";
        return null;
    }

    private static async Task<IResult> CreateWorkTask(WorkTaskRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (await db.WorkTasks.AnyAsync(x => x.Id == r.Id)) return Results.Conflict(new { error = "Bu görev daha önce kaydedildi. Listeyi yenileyin; ikinci bir görev oluşturulmadı." });
        var error = await TaskTargetError(r, db); if (error is not null) return Results.Conflict(new { error });
        var task = new WorkTask { Id = r.Id, BrandId = r.BrandId, AssigneeId = r.AssigneeId, Title = r.Title.Trim(), Description = r.Description.Trim(),
            Priority = r.Priority, Kind = r.Kind, DealId = r.DealId, Year = r.Year, Month = r.Month, DueOn = r.DueOn, CreatedBy = User(user) };
        db.Add(task); Audit(db, user, "WorkTaskCreated", "WorkTask", task.Id, null, task);
        await db.SaveChangesAsync(); return Results.Created($"/api/work-tasks?brandId={task.BrandId}", task);
    }

    private static async Task<IResult> UpdateWorkTask(Guid id, WorkTaskRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.FindAsync(id); if (task is null) return Results.NotFound();
        if (r.Id != id || r.BrandId != task.BrandId || r.Kind != task.Kind || r.DealId != task.DealId || r.Year != task.Year || r.Month != task.Month)
            return Results.Conflict(new { error = "Görevin marka ve bağlı kayıt bilgileri değiştirilemez." });
        if (task.Revision != r.Revision) return Results.Conflict(new { error = "Görev değişmiş. Listeyi yenileyip tekrar deneyin." });
        if (task.CompletedAt is not null) return Results.Conflict(new { error = "Tamamlanan görevi düzenlemeden önce yeniden açın." });
        var error = await TaskTargetError(r, db); if (error is not null) return Results.Conflict(new { error });
        var old = JsonSerializer.Serialize(task, Json);
        task.AssigneeId = r.AssigneeId; task.Title = r.Title.Trim(); task.Description = r.Description.Trim(); task.Priority = r.Priority; task.DueOn = r.DueOn; task.Revision++;
        Audit(db, user, "WorkTaskChanged", "WorkTask", id, old, task); await db.SaveChangesAsync(); return Results.Ok(task);
    }

    private static async Task<IResult> CompleteWorkTask(Guid id, WorkCompletionRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.FindAsync(id); if (task is null) return Results.NotFound();
        if (!user.IsInRole("Admin") && !user.IsInRole("Partner") && task.AssigneeId != Guid.Parse(user.FindFirstValue("uid")!)) return Results.Forbid();
        if (task.Revision != r.Revision) return Results.Conflict(new { error = "Görev değişmiş. Listeyi yenileyip tekrar deneyin." });
        if ((task.CompletedAt is not null) == r.Completed) return Results.Ok(task);
        var old = JsonSerializer.Serialize(task, Json);
        task.CompletedAt = r.Completed ? DateTimeOffset.UtcNow : null; task.CompletedBy = r.Completed ? User(user) : ""; task.Revision++;
        Audit(db, user, r.Completed ? "WorkTaskCompleted" : "WorkTaskReopened", "WorkTask", id, old, task);
        await db.SaveChangesAsync(); return Results.Ok(task);
    }

    private static async Task<IResult> SaveFollowUp(Guid id, FollowUpRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == id)) return Results.NotFound();
        if (r.OwnerId.HasValue && !await db.UserAccounts.AnyAsync(x => x.Id == r.OwnerId && x.IsActive && x.Role != "BrandClient")) return Results.Conflict(new { error = "Marka sorumlusu olarak etkin bir çalışan seçin." });
        var follow = await db.BrandFollowUps.FindAsync(id);
        if ((follow?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Takip bilgileri değişmiş. Sayfayı yenileyin." });
        var old = follow is null ? null : JsonSerializer.Serialize(follow, Json);
        if (follow is null) { follow = new BrandFollowUp { BrandId = id }; db.Add(follow); }
        follow.OwnerId = r.OwnerId; follow.Stage = r.Stage; follow.WaitingReason = r.WaitingReason.Trim(); follow.NextContactOn = r.NextContactOn;
        follow.NextStep = r.NextStep.Trim(); follow.Revision++;
        Audit(db, user, "BrandFollowUpChanged", "Brand", id, old, follow); await db.SaveChangesAsync(); return Results.Ok(follow);
    }

    private static async Task<IResult> AddContactNote(Guid id, ContactNoteRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == id)) return Results.NotFound();
        if (r.ContactOn > TeamWork.Today(DateTimeOffset.UtcNow)) return Results.BadRequest(new { error = "Gerçekleşen görüşme tarihi gelecekte olamaz. Planlanan görüşmeyi sonraki görüşme alanına yazın." });
        if (await db.BrandContactNotes.AnyAsync(x => x.Id == r.Id)) return Results.Conflict(new { error = "Bu not daha önce kaydedildi. Listeyi yenileyin." });
        var note = new BrandContactNote { Id = r.Id, BrandId = id, Text = r.Text.Trim(), ContactOn = r.ContactOn, CreatedBy = User(user) };
        db.Add(note); Audit(db, user, "BrandContactNoteAdded", "Brand", id, null, note); await db.SaveChangesAsync(); return Results.Created($"/api/brands/{id}/contact-notes", note);
    }
}
