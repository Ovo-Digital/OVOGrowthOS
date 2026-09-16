using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapPerformance(WebApplication app)
    {
        var group = app.MapGroup("/api/performance").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 20, string? search = null,
            MonthlyPerformanceStatus? status = null, int? year = null, int? month = null, string sort = "recent") =>
        {
            var q = db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Adjustments).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => EF.Functions.ILike(x.Brand!.Name, $"%{search}%"));
            if (status.HasValue) q = q.Where(x => x.Status == status);
            if (year.HasValue) q = q.Where(x => x.Year == year);
            if (month.HasValue) q = q.Where(x => x.Month == month);
            var ordered = sort switch { "oldest" => q.OrderBy(x => x.Year).ThenBy(x => x.Month), "name" => q.OrderBy(x => x.Brand!.Name),
                "nameDesc" => q.OrderByDescending(x => x.Brand!.Name), _ => q.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month) };
            return Results.Ok(await Page(ordered, page, pageSize));
        });
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Adjustments)
                .SingleOrDefaultAsync(x => x.Id == id) is { } p ? Results.Ok(p) : Results.NotFound());
        group.MapPost("/calculate", async (PerformanceRequest request, AppDbContext db) =>
        {
            var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active);
            if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." });
            if (deal.BrandId != request.BrandId) return Results.Conflict(new { error = "Seçilen anlaşma bu markaya ait değildir." });
            var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); return Results.Ok(p);
        }).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/", async (PerformanceRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var deal = await db.Deals.SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active);
            if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." });
            if (deal.BrandId != request.BrandId) return Results.Conflict(new { error = "Seçilen anlaşma bu markaya ait değildir." });
            if (await db.MonthlyPerformances.AnyAsync(x => x.BrandId == request.BrandId && x.Year == request.Year && x.Month == request.Month))
                return Results.Conflict(new { error = "Bu marka ve dönem için daha önce kayıt oluşturulmuş." });
            var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); TouchPeriod(p);
            db.Add(p); Audit(db, user, "MonthlyPerformanceCreated", "MonthlyPerformance", p.Id, null, p);
            await db.SaveChangesAsync(); return Results.Created($"/api/performance/{p.Id}", p);
        }).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPut("/{id:guid}", SavePerformance).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/preview", PreviewPerformance).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        foreach (var operation in new[] { "submit", "approve", "lock" })
        {
            var action = operation;
            group.MapPost("/{id:guid}/" + action, (Guid id, HttpRequest http, AppDbContext db, ClaimsPrincipal user) =>
                MovePerformance(id, action, new TransitionRequest(), http, db, user)).RequireAuthorization("OperationsWrite");
        }
        group.MapPost("/{id:guid}/return", (Guid id, TransitionRequest request, HttpRequest http, AppDbContext db, ClaimsPrincipal user) =>
            MovePerformance(id, "return", request, http, db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/unlock", (Guid id, TransitionRequest request, HttpRequest http, AppDbContext db, ClaimsPrincipal user) =>
            MovePerformance(id, "unlock", request, http, db, user)).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/adjustments", AddPerformanceAdjustment).AddEndpointFilter<ValidationFilter<AdjustmentRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/invoice", () => Results.Conflict(new { error = "Hakediş dökümündeki fatura ve tahsilat formunu kullanın." })).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/pay", () => Results.Conflict(new { error = "Hakediş dökümünden ödeme tutarı, tarihi ve referansı ile tahsilat kaydedin." })).RequireAuthorization("OperationsWrite");
        app.MapGet("/api/commissions", ListCommissions).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> SavePerformance(Guid id, PerformanceRequest request, HttpRequest http, AppDbContext db, ClaimsPrincipal user)
    {
        var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (EditablePeriodError(p, request, http) is { } error) return error;
        var old = JsonSerializer.Serialize(p, Json);
        Copy(p, request); MonthlyCloseWorkflow.ClearReview(p); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); TouchPeriod(p);
        Audit(db, user, "MonthlyPerformanceChanged", "MonthlyPerformance", id, old, p);
        await db.SaveChangesAsync(); return Results.Ok(p);
    }

    private static async Task<IResult> PreviewPerformance(Guid id, PerformanceRequest request, HttpRequest http, AppDbContext db)
    {
        var p = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (EditablePeriodError(p, request, http) is { } error) return error;
        Copy(p, request); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); return Results.Ok(p);
    }

    private static IResult? EditablePeriodError(MonthlyPerformance p, PerformanceRequest request, HttpRequest http)
    {
        if (!MonthlyCloseWorkflow.CanEdit(p.Status)) return Results.Conflict(new { error = "Yalnız taslak dönem düzenlenebilir. Kontroldeki veya onaylı kaydı önce gerekçeyle taslağa gönderin." });
        if (p.BrandId != request.BrandId || p.DealId != request.DealId || p.Year != request.Year || p.Month != request.Month)
            return Results.Conflict(new { error = "Kayıtlı dönemin markası, anlaşması, yılı veya ayı değiştirilemez." });
        return PeriodVersionError(p, http);
    }

    private static IResult? PeriodVersionError(MonthlyPerformance p, HttpRequest http)
    {
        // The client returns the exact updatedAt from its loaded record, not a JavaScript-rounded date.
        var expected = http.Headers.IfMatch.ToString().Trim('"');
        if (string.IsNullOrWhiteSpace(expected)) return Results.Problem(statusCode: 428, title: "Güncel kayıt bilgisi gerekli. Sayfayı yenileyip tekrar deneyin.");
        if (!DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var version) || version != p.UpdatedAt)
            return Results.Conflict(new { error = "Bu kayıt siz açtıktan sonra değişti. Sayfayı yenileyip son bilgileri kontrol edin; işleminiz kaydedilmedi." });
        return null;
    }

    private static void TouchPeriod(MonthlyPerformance p)
    {
        // PostgreSQL stores microseconds. Keep the response token identical after the next database read.
        var ticks = Math.Max(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, p.UpdatedAt.UtcTicks / 10 * 10 + 10);
        p.UpdatedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static async Task<IResult> MovePerformance(Guid id, string action, TransitionRequest request, HttpRequest http, AppDbContext db, ClaimsPrincipal user)
    {
        var p = await db.MonthlyPerformances.FindAsync(id);
        if (p is null) return Results.NotFound();
        var permitted = action switch
        {
            "submit" => MonthlyCloseWorkflow.CanTransition(p.Status, MonthlyPerformanceStatus.UnderReview),
            "approve" => MonthlyCloseWorkflow.CanTransition(p.Status, MonthlyPerformanceStatus.Approved),
            "lock" => MonthlyCloseWorkflow.CanTransition(p.Status, MonthlyPerformanceStatus.Locked),
            "return" => MonthlyCloseWorkflow.CanReturn(p.Status),
            "unlock" => MonthlyCloseWorkflow.CanUnlock(p.Status),
            _ => false
        };
        if (!permitted) return Results.Conflict(new { error = "Kayıt bu işlem için uygun aşamada değil. Faturalanmış ve ödenmiş dönemler açılamaz." });
        if (action is "return" or "unlock" && (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["1–1.000 karakterlik açık bir gerekçe yazın."] });
        if (action == "approve")
        {
            // Keep identity across an account email change; older submissions still use their recorded email.
            var submission = await db.AuditRecords.AsNoTracking().Where(a => a.EntityType == "MonthlyPerformance"
                && a.EntityId == id.ToString() && a.Action == "MonthlyPerformanceSubmitted")
                .OrderByDescending(a => a.CreatedAt).Select(a => a.NewValueJson).FirstOrDefaultAsync();
            var submittingUserId = submission is null ? null : JsonSerializer.Deserialize<JsonElement>(submission)
                .TryGetProperty("submittedByUserId", out var uid) ? uid.GetString() : null;
            if (string.Equals(p.PreparedBy, User(user), StringComparison.OrdinalIgnoreCase)
                || (submittingUserId is not null && submittingUserId == user.FindFirstValue("uid")))
                return Results.Conflict(new { error = "Aylık sonucu hazırlayan kişi aynı kaydı onaylayamaz. Başka bir yetkili onaylamalıdır." });
        }
        if (PeriodVersionError(p, http) is { } error) return error;
        var old = new { p.Status, p.PreparedBy, p.SubmittedAt, p.ReviewedBy, p.ApprovedAt, p.LockedAt };
        var now = DateTimeOffset.UtcNow;
        var audit = action switch
        {
            "submit" => "MonthlyPerformanceSubmitted", "approve" => "MonthlyPerformanceApproved", "lock" => "MonthlyCloseLocked",
            "return" => "MonthlyPerformanceReturned", _ => "MonthlyCloseUnlocked"
        };
        switch (action)
        {
            case "submit": p.Status = MonthlyPerformanceStatus.UnderReview; p.PreparedBy = User(user); p.SubmittedAt = now; break;
            case "approve": p.Status = MonthlyPerformanceStatus.Approved; p.ReviewedBy = User(user); p.ApprovedAt = now; break;
            case "lock": p.Status = MonthlyPerformanceStatus.Locked; p.LockedAt = now; break;
            default: MonthlyCloseWorkflow.ClearReview(p); break;
        }
        TouchPeriod(p);
        Audit(db, user, audit, "MonthlyPerformance", id, old,
            new { p.Status, p.PreparedBy, p.SubmittedAt, p.ReviewedBy, p.ApprovedAt, p.LockedAt,
                submittedByUserId = action == "submit" ? user.FindFirstValue("uid") : null }, request.Reason?.Trim() ?? "");
        await db.SaveChangesAsync(); return Results.Ok(p);
    }

    private static async Task<IResult> AddPerformanceAdjustment(Guid id, AdjustmentRequest request, HttpRequest http, AppDbContext db, ClaimsPrincipal user)
    {
        var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!MonthlyCloseWorkflow.CanAdjust(p.Status)) return Results.Conflict(new { error = "Hakediş düzeltmesi yalnız taslak döneme eklenebilir. Önce yetkili kişi kaydı gerekçeyle taslağa göndermelidir." });
        if (PeriodVersionError(p, http) is { } error) return error;
        var a = new CommissionAdjustment { MonthlyPerformanceId = id, Amount = request.Amount, Reason = request.Reason.Trim(), CreatedBy = User(user) };
        p.Adjustments.Add(a); db.CommissionAdjustments.Add(a); MonthlyCloseWorkflow.ClearReview(p);
        MonthlyPerformanceCalculator.Calculate(p, p.Deal!); TouchPeriod(p);
        Audit(db, user, "CommissionAdjusted", "MonthlyPerformance", id, null, a, request.Reason);
        await db.SaveChangesAsync(); return Results.Ok(new { adjustment = a, p.OvoFee, p.CommissionBreakdownJson });
    }

    private static MonthlyPerformance ToPerformance(PerformanceRequest r)
    {
        var p = new MonthlyPerformance { BrandId = r.BrandId, DealId = r.DealId, Year = r.Year, Month = r.Month };
        Copy(p, r); return p;
    }
    private static void Copy(MonthlyPerformance p, PerformanceRequest r)
    {
        p.GrossSales = r.GrossSales; p.Vat = r.Vat; p.Refunds = r.Refunds; p.Cancellations = r.Cancellations;
        p.Chargebacks = r.Chargebacks; p.CustomerPaidShipping = r.CustomerPaidShipping; p.GiftCardTopups = r.GiftCardTopups;
        p.Orders = r.Orders; p.Sessions = r.Sessions; p.NewCustomers = r.NewCustomers; p.ReturningCustomers = r.ReturningCustomers;
        p.Cogs = r.Cogs; p.PaymentFees = r.PaymentFees; p.FulfillmentCosts = r.FulfillmentCosts; p.ShippingSubsidy = r.ShippingSubsidy;
        p.OtherVariableCosts = r.OtherVariableCosts; p.MetaSpend = r.MetaSpend; p.GoogleSpend = r.GoogleSpend;
        p.TikTokSpend = r.TikTokSpend; p.InfluencerSpend = r.InfluencerSpend; p.OtherAdSpend = r.OtherAdSpend;
    }
}
