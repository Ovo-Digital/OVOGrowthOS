using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record PortalAccountRequest(string Email, string Name, string? Password, bool IsActive = true);
public sealed record PortalPublishRequest(Guid PerformanceId);
public sealed record PortalShareRequest(Guid DocumentId);
public sealed record PortalQuestionRequest(Guid ReportId, string Question);
public sealed record PortalAnswerRequest(string Answer);

public sealed class PortalAccountRequestValidator : AbstractValidator<PortalAccountRequest>
{
    public PortalAccountRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Ad soyad zorunludur; en fazla 160 karakter kullanın.");
        RuleFor(x => x.Password).Must(x => x is null or "" || !string.IsNullOrWhiteSpace(x) && x.Length is >= 10 and <= 256).WithMessage("Şifre 10–256 karakter arasında olmalıdır.");
    }
}

public static partial class WorkflowEndpoints
{
    private static void MapCustomerPortal(WebApplication app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization("PortalAccess");
        portal.MapGet("/", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var brandId = await PortalBrand(db, user); var actor = Guid.Parse(user.FindFirstValue("uid")!);
            var brand = await db.Brands.Where(x => x.Id == brandId).Select(x => new { x.Name }).SingleAsync();
            return Results.Ok(new { brandName = brand.Name,
                reports = await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId && x.RevokedAt == null).OrderByDescending(x => x.PublishedAt)
                    .Select(x => new { x.Id, x.Year, x.Month, x.Version, x.PublishedAt }).ToListAsync(),
                documents = await db.PortalDocumentShares.AsNoTracking().Where(x => x.BrandId == brandId && x.RevokedAt == null).OrderByDescending(x => x.SharedAt)
                    .Select(x => new { x.Id, x.Document.FileName, x.SharedAt }).ToListAsync(),
                questions = await db.PortalQuestions.AsNoTracking().Where(x => x.BrandId == brandId && x.UserId == actor)
                    .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.ReportId, x.Question, x.Answer, x.CreatedAt, x.AnsweredAt }).ToListAsync() });
        });
        portal.MapGet("/reports/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brandId = await PortalBrand(db, user);
            var report = await db.PortalReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId && x.RevokedAt == null);
            return report is null ? Results.NotFound() : Results.Ok(ReadPortalSnapshot(report));
        });
        portal.MapGet("/reports/{id:guid}/csv", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brandId = await PortalBrand(db, user);
            var report = await db.PortalReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId && x.RevokedAt == null);
            if (report is null) return Results.NotFound();
            var s = ReadPortalSnapshot(report);
            var csv = BrandReportCsv(new BrandReportDocument { BrandName = s.BrandName, Currency = s.Currency, Year = report.Year, Month = report.Month,
                Scope = "Closed", Audience = "brand", GeneratedAt = s.PublishedAt, Current = s.Metrics, Explanations = s.Explanations });
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes($"\"Yayımlanan sürüm\";\"{s.Version}\"\r\n" + csv)).ToArray(), "text/csv; charset=utf-8", $"rapor-{report.Year}-{report.Month:00}-v{report.Version}.csv");
        });
        portal.MapGet("/documents/{id:guid}/download", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brandId = await PortalBrand(db, user);
            var share = await db.PortalDocumentShares.AsNoTracking().Include(x => x.Document).SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId && x.RevokedAt == null);
            return share is null ? Results.NotFound() : Results.File(share.Document.Content, "application/octet-stream", share.Document.FileName);
        });
        portal.MapPost("/questions", async (PortalQuestionRequest r, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(r.Question) || r.Question.Length > 2000) return Results.BadRequest(new { error = "Sorunuzu 1–2000 karakter arasında yazın." });
            var brandId = await PortalBrand(db, user);
            if (!await db.PortalReports.AnyAsync(x => x.Id == r.ReportId && x.BrandId == brandId && x.RevokedAt == null)) return Results.NotFound();
            var actor = Guid.Parse(user.FindFirstValue("uid")!);
            var since = DateTimeOffset.UtcNow.AddHours(-1);
            if (await db.PortalQuestions.CountAsync(x => x.UserId == actor && x.CreatedAt >= since) >= 20)
                return Results.Json(new { error = "Son bir saatte 20 soru gönderdiniz. Lütfen yanıtları bekleyin." }, statusCode: 429);
            var q = new PortalQuestion { BrandId = brandId, ReportId = r.ReportId, UserId = actor, Question = r.Question.Trim() };
            db.Add(q); Audit(db, user, "PortalQuestionCreated", "PortalQuestion", q.Id, null, new { q.BrandId, q.ReportId }); await db.SaveChangesAsync();
            return Results.Created($"/api/portal/questions/{q.Id}", new { q.Id });
        });

        var management = app.MapGroup("/api/portal-management/brands/{brandId:guid}").RequireAuthorization("OperationsWrite");
        management.MapGet("/accounts", async (Guid brandId, AppDbContext db) => Results.Ok(await db.PortalAccesses.AsNoTracking().Where(x => x.BrandId == brandId)
            .Select(x => new { id = x.UserId, x.User.Name, x.User.Email, x.User.IsActive }).ToListAsync())).RequireAuthorization("AdminOnly");
        management.MapPost("/accounts", async (Guid brandId, PortalAccountRequest r, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(r.Password)) return Results.BadRequest(new { error = "Yeni müşteri hesabı için şifre girin." });
            if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.UserAccounts.AnyAsync(x => x.Email == email)) return Results.Conflict(new { error = "Bu e-posta zaten kullanılıyor. İç ekip hesabını müşteri hesabına dönüştürmeyin." });
            var account = new UserAccount { Email = email, Name = r.Name.Trim(), Role = "BrandClient", PasswordHash = JwtTokenService.HashPassword(r.Password), IsActive = r.IsActive };
            db.Add(new PortalAccess { User = account, UserId = account.Id, BrandId = brandId });
            Audit(db, user, "PortalAccountCreated", "Brand", brandId, null, new { account.Id, account.Email, account.Name, account.IsActive });
            await db.SaveChangesAsync(); return Results.Created($"/api/portal-management/brands/{brandId}/accounts/{account.Id}", new { account.Id });
        }).AddEndpointFilter<ValidationFilter<PortalAccountRequest>>().RequireAuthorization("AdminOnly");
        management.MapPut("/accounts/{id:guid}", async (Guid brandId, Guid id, PortalAccountRequest r, AppDbContext db, ClaimsPrincipal user) =>
        {
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            if (tx is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"UserAccounts\" IN SHARE ROW EXCLUSIVE MODE");
            var actorId = Guid.Parse(user.FindFirstValue("uid")!);
            var actorVersion = int.Parse(user.FindFirstValue("session_version")!, System.Globalization.CultureInfo.InvariantCulture);
            if (!await db.UserAccounts.AnyAsync(x => x.Id == actorId && x.IsActive && x.Role == "Admin" && x.TokenVersion == actorVersion))
                return Results.Unauthorized();
            var access = await db.PortalAccesses.Include(x => x.User).SingleOrDefaultAsync(x => x.BrandId == brandId && x.UserId == id && x.User.Role == "BrandClient");
            if (access is null) return Results.NotFound();
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.UserAccounts.AnyAsync(x => x.Id != id && x.Email == email)) return Results.Conflict(new { error = "Bu e-posta zaten kullanılıyor." });
            access.User.Email = email; access.User.Name = r.Name.Trim(); access.User.IsActive = r.IsActive; access.User.TokenVersion++;
            access.User.UpdatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(r.Password)) access.User.PasswordHash = JwtTokenService.HashPassword(r.Password);
            Audit(db, user, "PortalAccountChanged", "Brand", brandId, null, new { id, email, r.Name, r.IsActive });
            await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<PortalAccountRequest>>().RequireAuthorization("AdminOnly");
        management.MapGet("/reports", async (Guid brandId, AppDbContext db) => Results.Ok(await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId)
            .OrderByDescending(x => x.PublishedAt).Select(x => new { x.Id, x.Year, x.Month, x.Version, x.PublishedAt, x.RevokedAt }).ToListAsync()));
        management.MapGet("/periods", async (Guid brandId, AppDbContext db) => Results.Ok(await db.MonthlyPerformances.AsNoTracking().Where(x => x.BrandId == brandId &&
            (x.Status == MonthlyPerformanceStatus.Locked || x.Status == MonthlyPerformanceStatus.Invoiced || x.Status == MonthlyPerformanceStatus.Paid))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Select(x => new { x.Id, x.Year, x.Month, x.Deal!.Currency }).ToListAsync()));
        management.MapGet("/reports/{id:guid}", async (Guid brandId, Guid id, AppDbContext db) =>
            await db.PortalReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId) is { } r ? Results.Ok(ReadPortalSnapshot(r)) : Results.NotFound());
        management.MapPost("/reports", PublishPortalReport);
        management.MapPost("/reports/{id:guid}/revoke", async (Guid brandId, Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var report = await db.PortalReports.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId); if (report is null) return Results.NotFound();
            if (report.RevokedAt is not null) return Results.NoContent(); report.RevokedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "PortalReportRevoked", "Brand", brandId, null, new { id }); await db.SaveChangesAsync(); return Results.NoContent();
        });
        management.MapGet("/documents", async (Guid brandId, AppDbContext db) => Results.Ok(await db.PortalDocumentShares.AsNoTracking().Where(x => x.BrandId == brandId)
            .OrderByDescending(x => x.SharedAt).Select(x => new { x.Id, x.DocumentId, x.Document.FileName, x.SharedAt, x.RevokedAt }).ToListAsync()));
        management.MapPost("/documents", async (Guid brandId, PortalShareRequest r, AppDbContext db, ClaimsPrincipal user) =>
        {
            // Only documents attached directly to this brand are eligible. Notes are never shared.
            if (!await db.DocumentAttachments.AnyAsync(x => x.Id == r.DocumentId && x.EntityType == "Brand" && x.EntityId == brandId.ToString())) return Results.NotFound();
            if (await db.PortalDocumentShares.AnyAsync(x => x.DocumentId == r.DocumentId && x.RevokedAt == null)) return Results.Conflict(new { error = "Belge zaten paylaşılıyor." });
            var share = new PortalDocumentShare { BrandId = brandId, DocumentId = r.DocumentId }; db.Add(share);
            Audit(db, user, "PortalDocumentShared", "Brand", brandId, null, new { share.Id, r.DocumentId }); await db.SaveChangesAsync(); return Results.Ok(new { share.Id });
        });
        management.MapPost("/documents/{id:guid}/revoke", async (Guid brandId, Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var share = await db.PortalDocumentShares.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId); if (share is null) return Results.NotFound();
            if (share.RevokedAt is not null) return Results.NoContent(); share.RevokedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "PortalDocumentRevoked", "Brand", brandId, null, new { id }); await db.SaveChangesAsync(); return Results.NoContent();
        });
        management.MapGet("/questions", async (Guid brandId, AppDbContext db) => Results.Ok(await db.PortalQuestions.AsNoTracking().Where(x => x.BrandId == brandId)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.ReportId, x.Question, x.Answer, x.CreatedAt, x.AnsweredAt }).ToListAsync()));
        management.MapPost("/questions/{id:guid}/answer", async (Guid brandId, Guid id, PortalAnswerRequest r, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(r.Answer) || r.Answer.Length > 4000) return Results.BadRequest(new { error = "Yanıtınızı 1–4000 karakter arasında yazın." });
            var q = await db.PortalQuestions.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId); if (q is null) return Results.NotFound();
            if (q.AnsweredAt is not null) return Results.Conflict(new { error = "Bu soru zaten yanıtlandı. Yayımlanan yanıt değiştirilmez." });
            q.Answer = r.Answer.Trim(); q.AnsweredAt = DateTimeOffset.UtcNow;
            Audit(db, user, "PortalQuestionAnswered", "Brand", brandId, null, new { id }); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }

    private static Task<Guid> PortalBrand(AppDbContext db, ClaimsPrincipal user)
    {
        var id = Guid.Parse(user.FindFirstValue("uid")!);
        return db.PortalAccesses.Where(x => x.UserId == id).Select(x => x.BrandId).SingleAsync();
    }
    private static PortalReportSnapshot ReadPortalSnapshot(PortalReport report) => JsonSerializer.Deserialize<PortalReportSnapshot>(report.SnapshotJson, Json)!;

    private static async Task<IResult> PublishPortalReport(Guid brandId, PortalPublishRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null)
        {
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"PortalReports\" IN SHARE ROW EXCLUSIVE MODE");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"MonthlyPerformances\" WHERE \"Id\" = {r.PerformanceId} FOR SHARE");
        }
        var p = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
            .SingleOrDefaultAsync(x => x.Id == r.PerformanceId && x.BrandId == brandId);
        if (p is null) return Results.NotFound();
        if (!PortfolioReporting.Matches(p.Status, ReportScope.Closed)) return Results.Conflict(new { error = "Yalnız kilitlenmiş, faturalanmış veya ödenmiş dönem yayımlanabilir." });
        var version = (await db.PortalReports.Where(x => x.BrandId == brandId && x.Year == p.Year && x.Month == p.Month).Select(x => (int?)x.Version).MaxAsync() ?? 0) + 1;
        var report = new PortalReport { BrandId = brandId, PerformanceId = p.Id, Year = p.Year, Month = p.Month, Version = version };
        var metrics = BrandReporting.Metrics(p);
        report.SnapshotJson = JsonSerializer.Serialize(new PortalReportSnapshot(p.Brand!.Name, p.Deal!.Currency, version, report.PublishedAt, metrics, BrandReporting.Explain(metrics, null, false, previousIncluded: false)), Json);
        db.Add(report); Audit(db, user, "PortalReportPublished", "Brand", brandId, null, new { report.Id, report.Year, report.Month, report.Version });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Created($"/api/portal-management/brands/{brandId}/reports/{report.Id}", new { report.Id, report.Version });
    }
}
