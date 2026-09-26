using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record PipelineLossRequest(string Reason);

public static partial class WorkflowEndpoints
{
    private static readonly BrandStatus[] PipelineStatuses = [BrandStatus.Lead, BrandStatus.Evaluation, BrandStatus.Negotiation];

    private static void MapPipeline(WebApplication app)
    {
        app.MapGet("/api/pipeline/summary", PipelineSummary).RequireAuthorization("ReadAccess");
        app.MapGet("/api/brands/{id:guid}/stage-history", StageHistory).RequireAuthorization("ReadAccess");
        app.MapPost("/api/brands/{id:guid}/pipeline/loss", RecordPipelineLoss)
            .AddEndpointFilter<ValidationFilter<PipelineLossRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPost("/api/brands/{id:guid}/pipeline/loss/cancel", CancelPipelineLoss).RequireAuthorization("OperationsWrite");
    }

    private static async Task<IResult> StageHistory(Guid id, AppDbContext db)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == id)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var rows = await db.BrandStageHistories.AsNoTracking().Where(x => x.BrandId == id)
            .OrderByDescending(x => x.EnteredAt).ThenByDescending(x => x.Id).ToListAsync();
        return Results.Ok(new
        {
            brandId = id,
            items = rows.Select(x => new
            {
                x.Id, x.Stage, x.EntryKnown, x.EnteredAt, x.ExitedAt, x.EnteredBy, x.ExitedBy, x.Note,
                days = Pipeline.StageDays(x, now)
            })
        });
    }

    private static async Task<IResult> RecordPipelineLoss(Guid id, PipelineLossRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (brand is null) return Results.NotFound();
        var follow = await db.BrandFollowUps.SingleOrDefaultAsync(x => x.BrandId == id);
        var hasDeal = await db.Deals.AnyAsync(x => x.BrandId == id);
        var error = Pipeline.LossError(follow?.LostOn is not null, hasDeal, r.Reason);
        if (error is not null) return Results.Conflict(new { error });
        if (follow is null) { follow = new BrandFollowUp { BrandId = id }; db.BrandFollowUps.Add(follow); }
        follow.LostOn = TeamWork.Today(DateTimeOffset.UtcNow);
        follow.LostReason = r.Reason.Trim();
        follow.LostBy = User(user);
        follow.Revision++;
        Audit(db, user, "BrandPipelineLossRecorded", "Brand", id, null, new { follow.LostOn, reason = follow.LostReason });
        await db.SaveChangesAsync();
        return Results.Ok(new { follow.BrandId, follow.LostOn, follow.LostReason, follow.LostBy, follow.Revision });
    }

    private static async Task<IResult> CancelPipelineLoss(Guid id, AppDbContext db, ClaimsPrincipal user)
    {
        var follow = await db.BrandFollowUps.SingleOrDefaultAsync(x => x.BrandId == id);
        var error = Pipeline.CancelLossError(follow?.LostOn is not null);
        if (error is not null) return Results.Conflict(new { error });
        var old = follow!.LostReason;
        follow.LostOn = null; follow.LostReason = ""; follow.LostBy = ""; follow.Revision++;
        Audit(db, user, "BrandPipelineLossCancelled", "Brand", id, new { reason = old }, new { follow.Revision });
        await db.SaveChangesAsync();
        return Results.Ok(new { follow.BrandId, follow.LostOn, follow.Revision });
    }

    private static async Task<IResult> PipelineSummary(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var allBrands = await db.Brands.AsNoTracking().Select(x => new { x.Id, x.Name, x.Status }).ToListAsync();
        var pipelineBrands = allBrands.Where(x => PipelineStatuses.Contains(x.Status)).ToList();
        var follows = await db.BrandFollowUps.AsNoTracking().ToDictionaryAsync(x => x.BrandId);
        var history = (await db.BrandStageHistories.AsNoTracking().ToListAsync()).GroupBy(x => x.BrandId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var deals = (await db.Deals.AsNoTracking().Select(x => x.BrandId).ToListAsync()).GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        BrandFollowUp? Follow(Guid brandId) => follows.GetValueOrDefault(brandId);
        List<BrandStageHistory>? History(Guid brandId) => history.GetValueOrDefault(brandId);

        var openPipeline = pipelineBrands.Where(x => Follow(x.Id)?.LostOn is null).ToList();
        var lostPipeline = pipelineBrands.Where(x => Follow(x.Id)?.LostOn is not null).ToList();

        var current = openPipeline.Select(brand =>
        {
            var rows = History(brand.Id);
            var openRow = rows?.FirstOrDefault(x => x.ExitedAt is null);
            var stage = Follow(brand.Id)?.Stage ?? LeadStage.New;
            return (Stage: stage, Days: openRow is null ? null : Pipeline.StageDays(openRow, now));
        }).ToList();
        var stages = Enum.GetValues<LeadStage>().Select(stage => Pipeline.Wait(current, stage)).ToList();

        var sources = pipelineBrands.GroupBy(x => Follow(x.Id)?.SourceChannel ?? LeadSource.Unspecified)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new { channel = g.Key, label = Pipeline.SourceLabel(g.Key), count = g.Count() }).ToList();

        var measured = allBrands.Where(x => history.ContainsKey(x.Id)).ToList();
        var reached = measured.Where(x =>
            History(x.Id)!.Any(row => row.Stage == LeadStage.ProposalFollowUp)
            || Follow(x.Id)?.Stage == LeadStage.ProposalFollowUp
            || deals.ContainsKey(x.Id)).ToList();
        var converted = reached.Where(x => deals.ContainsKey(x.Id)).ToList();
        var beforeMeasurement = allBrands.Count(x => !history.ContainsKey(x.Id) && deals.ContainsKey(x.Id));
        var notMeasured = openPipeline.Count(x => !history.ContainsKey(x.Id) && !deals.ContainsKey(x.Id));

        var losses = lostPipeline.GroupBy(x => Follow(x.Id)!.LostReason)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new { reason = g.Key, count = g.Count() }).ToList();

        var renewalPrep = await db.WorkTasks.AsNoTracking().Include(x => x.Brand).Include(x => x.Assignee)
            .Where(x => x.Kind == WorkKind.ContractRenewal)
            .OrderBy(x => x.DueOn).ThenBy(x => x.Id).Take(10).ToListAsync();

        return Results.Ok(new
        {
            generatedAt = now,
            open = openPipeline.Count,
            lost = lostPipeline.Count,
            stages = stages.Select(x => new
            {
                stage = x.Stage, open = x.Open, known = x.Known, unknown = x.Unknown,
                averageDays = x.AverageDays, longestDays = x.LongestDays
            }),
            sources,
            conversion = new
            {
                reached = reached.Count, converted = converted.Count,
                rate = Pipeline.ConversionRate(reached.Count, converted.Count),
                beforeMeasurement, notMeasured
            },
            losses,
            renewalPrep = renewalPrep.Select(x => new
            {
                taskId = x.Id, brandId = x.BrandId, brandName = x.Brand!.Name, x.Title, x.DueOn,
                assigneeName = x.Assignee!.Name, completed = x.CompletedAt is not null
            }),
            notes = new[]
            {
                "Aşama bekleme süreleri yalnız ölçüm başlangıcından sonra girilen kayıtlardan hesaplanır; bilinmeyen gün sayısı tahmin edilmez.",
                "Takip aşaması markanın değerlendirme veya anlaşma durumunu değiştirmez ve anlaşma oluşturmaz.",
                "Dönüşüm oranına aynı marka birden fazla aşama kaydı olsa da yalnız bir kez girer.",
                "Ölçüm başlangıcından önce anlaşma yapılan markalar oranın dışında ayrıca gösterilir.",
                "Bu ölçümler satış garantisi veya çalışan performans puanı değildir; bordro değerlendirmesinde kullanılmaz.",
                "Yenileme öncesi görüşme hazırlığı mevcut görev üzerinden izlenir; ayrı bir görev türü açılmaz."
            }
        });
    }
}
