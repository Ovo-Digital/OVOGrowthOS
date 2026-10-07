using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapSalesChannels(WebApplication app)
    {
        var brands = app.MapGroup("/api/brands/{brandId:guid}/sales-channels").RequireAuthorization("ReadAccess");
        brands.MapGet("/", async (Guid brandId, AppDbContext db) =>
        {
            if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound(new { error = "Marka bulunamadı." });
            var items = await db.SalesChannels.AsNoTracking().Where(x => x.BrandId == brandId)
                .OrderBy(x => x.Name).Select(x => new { x.Id, x.BrandId, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            return Results.Ok(items);
        });
        brands.MapPost("/", async (Guid brandId, SalesChannelRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound(new { error = "Marka bulunamadı." });
            var name = request.Name.Trim();
            if (await db.SalesChannels.AnyAsync(x => x.BrandId == brandId && x.Name.ToLower() == name.ToLower()))
                return Results.Conflict(new { error = "Bu markada aynı adda bir satış kanalı zaten var." });
            if (await db.SalesChannels.CountAsync(x => x.BrandId == brandId) >= SalesChannelRules.MaxChannelsPerBrand)
                return Results.Conflict(new { error = $"Bir markada en fazla {SalesChannelRules.MaxChannelsPerBrand} satış kanalı olabilir." });
            var channel = new SalesChannel { BrandId = brandId, Name = name, IsActive = request.IsActive };
            db.Add(channel);
            Audit(db, user, "SalesChannelCreated", "SalesChannel", channel.Id, null, new { channel.BrandId, channel.Name, channel.IsActive });
            await db.SaveChangesAsync();
            return Results.Created($"/api/sales-channels/{channel.Id}", channel);
        }).AddEndpointFilter<ValidationFilter<SalesChannelRequest>>().RequireAuthorization("OperationsWrite");

        var channels = app.MapGroup("/api/sales-channels").RequireAuthorization("ReadAccess");
        channels.MapPut("/{id:guid}", async (Guid id, SalesChannelRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var channel = await db.SalesChannels.SingleOrDefaultAsync(x => x.Id == id);
            if (channel is null) return Results.NotFound(new { error = "Satış kanalı bulunamadı." });
            var name = request.Name.Trim();
            if (await db.SalesChannels.AnyAsync(x => x.BrandId == channel.BrandId && x.Id != id && x.Name.ToLower() == name.ToLower()))
                return Results.Conflict(new { error = "Bu markada aynı adda bir satış kanalı zaten var." });
            var old = new { channel.Name, channel.IsActive };
            channel.Name = name; channel.IsActive = request.IsActive; channel.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "SalesChannelChanged", "SalesChannel", id, old, new { channel.Name, channel.IsActive });
            await db.SaveChangesAsync();
            return Results.Ok(channel);
        }).AddEndpointFilter<ValidationFilter<SalesChannelRequest>>().RequireAuthorization("OperationsWrite");
        channels.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var channel = await db.SalesChannels.SingleOrDefaultAsync(x => x.Id == id);
            if (channel is null) return Results.NotFound(new { error = "Satış kanalı bulunamadı." });
            if (await db.DealChannelRates.AnyAsync(x => x.SalesChannelId == id)
                || await db.MonthlyPerformanceChannels.AnyAsync(x => x.SalesChannelId == id))
                return Results.Conflict(new { error = "Bu kanal anlaşma veya dönem kaydında kullanıldığı için silinemez. Kullanmayacaksanız kanalı pasif yapın." });
            db.Remove(channel);
            Audit(db, user, "SalesChannelDeleted", "SalesChannel", id, new { channel.BrandId, channel.Name }, null);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("OperationsWrite");

        var deals = app.MapGroup("/api/deals/{id:guid}/channel-rates").RequireAuthorization("ReadAccess");
        deals.MapGet("/", async (Guid id, AppDbContext db) =>
        {
            var deal = await db.Deals.AsNoTracking().Include(x => x.ChannelRates).ThenInclude(r => r.SalesChannel)
                .SingleOrDefaultAsync(x => x.Id == id);
            if (deal is null) return Results.NotFound(new { error = "Anlaşma bulunamadı." });
            return Results.Ok(deal.ChannelRates.OrderBy(r => r.SalesChannel!.Name)
                .Select(r => new { r.Id, r.DealId, r.SalesChannelId, ChannelName = r.SalesChannel!.Name, r.RevenueShareRate, r.UpdatedAt }));
        });
        deals.MapPut("/", async (Guid id, DealChannelRatesRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var deal = await db.Deals.Include(x => x.ChannelRates).SingleOrDefaultAsync(x => x.Id == id);
            if (deal is null) return Results.NotFound(new { error = "Anlaşma bulunamadı." });
            // Kanal oranları yalnız ileride hesaplanacak dönemleri etkiler; kilitli, faturalı ve ödenmiş
            // dönemler mühürlüdür. Bu yüzden etkin anlaşmada da güncellenebilir; kapanmış anlaşmada değişmez.
            if (deal.Status is DealStatus.Expired or DealStatus.Terminated or DealStatus.Rejected)
                return Results.Conflict(new { error = "Sona ermiş veya kapatılmış anlaşmanın kanal oranları değiştirilemez." });
            var channelIds = request.Rates.Select(x => x.SalesChannelId).Distinct().ToList();
            var brandChannels = await db.SalesChannels.AsNoTracking().Where(x => x.BrandId == deal.BrandId && channelIds.Contains(x.Id))
                .Select(x => x.Id).ToListAsync();
            if (brandChannels.Count != channelIds.Count)
                return Results.Conflict(new { error = "Seçilen satış kanallarından biri bu markaya ait değildir." });
            if (!DealCommissionCalculator.SupportsChannelRates(deal.DealType) && request.Rates.Count > 0)
                return Results.Conflict(new { error = "Kanal başına farklı oran yalnız sabit gelir payı içeren anlaşma modellerinde kullanılır." });
            var old = deal.ChannelRates.Select(r => new { r.SalesChannelId, r.RevenueShareRate }).ToList();
            foreach (var removed in deal.ChannelRates.Where(r => !channelIds.Contains(r.SalesChannelId)).ToList())
            {
                deal.ChannelRates.Remove(removed);
                db.DealChannelRates.Remove(removed);
            }
            foreach (var item in request.Rates)
            {
                var existing = deal.ChannelRates.SingleOrDefault(r => r.SalesChannelId == item.SalesChannelId);
                if (existing is null)
                {
                    // Koleksiyon ve DbSet'e birlikte eklenir (hakediş düzeltmelerindeki desen);
                    // yalnız koleksiyona ekleme bazı sağlayıcılarda durumu yanlış işaretler.
                    var rate = new DealChannelRate { DealId = id, SalesChannelId = item.SalesChannelId, RevenueShareRate = item.RevenueShareRate };
                    deal.ChannelRates.Add(rate);
                    db.DealChannelRates.Add(rate);
                }
                else { existing.RevenueShareRate = item.RevenueShareRate; existing.UpdatedAt = DateTimeOffset.UtcNow; }
            }
            deal.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "DealChannelRatesChanged", "Deal", id, old, request.Rates);
            await db.SaveChangesAsync();
            return Results.Ok(await db.DealChannelRates.AsNoTracking().Include(r => r.SalesChannel)
                .Where(r => r.DealId == id).OrderBy(r => r.SalesChannel!.Name)
                .Select(r => new { r.Id, r.DealId, r.SalesChannelId, ChannelName = r.SalesChannel!.Name, r.RevenueShareRate, r.UpdatedAt }).ToListAsync());
        }).AddEndpointFilter<ValidationFilter<DealChannelRatesRequest>>().RequireAuthorization("OperationsWrite");
    }
}
