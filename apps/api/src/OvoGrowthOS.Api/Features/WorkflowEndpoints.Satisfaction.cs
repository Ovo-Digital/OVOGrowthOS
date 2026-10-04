using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record SatisfactionRatingRequest(int Year, int Month, int Score, string? Comment);

public static partial class WorkflowEndpoints
{
    private static void MapSatisfaction(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/satisfaction", ReadSatisfaction).RequireAuthorization("ReadAccess");
        app.MapPost("/api/brands/{id:guid}/satisfaction", SaveSatisfaction).RequireAuthorization("OperationsWrite");
    }

    private static async Task<IResult> ReadSatisfaction(Guid id, AppDbContext db)
    {
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id)) return Results.NotFound();
        return Results.Ok(await SatisfactionPayload(db, id));
    }

    private static async Task<IResult> SaveSatisfaction(Guid id, SatisfactionRatingRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var error = SatisfactionRatings.ValidationError(r.Score, r.Comment, r.Year, r.Month);
        if (error is not null) return Results.BadRequest(new { error });
        if (!await db.Brands.AnyAsync(x => x.Id == id)) return Results.NotFound();
        var actor = User(user);
        var row = await db.SatisfactionRatings.SingleOrDefaultAsync(x => x.BrandId == id && x.Year == r.Year && x.Month == r.Month);
        if (row is null)
        {
            row = new SatisfactionRating { BrandId = id, Year = r.Year, Month = r.Month, Score = r.Score,
                Comment = r.Comment?.Trim() ?? "", CreatedBy = actor, UpdatedBy = actor };
            db.Add(row);
            Audit(db, user, "SatisfactionRatingCreated", "Brand", id, null, new { r.Year, r.Month, r.Score });
        }
        else
        {
            var before = new { row.Score, row.Comment };
            row.Score = r.Score; row.Comment = r.Comment?.Trim() ?? ""; row.UpdatedBy = actor; row.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "SatisfactionRatingChanged", "Brand", id, before, new { r.Year, r.Month, r.Score });
        }
        await db.SaveChangesAsync();
        return Results.Ok(await SatisfactionPayload(db, id));
    }

    private static async Task<object> SatisfactionPayload(AppDbContext db, Guid brandId)
    {
        var rows = await db.SatisfactionRatings.AsNoTracking().Where(x => x.BrandId == brandId)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync();
        return new
        {
            average = SatisfactionRatings.Average(rows.Select(x => x.Score)),
            count = rows.Count,
            items = rows.Select(x => new
            {
                x.Id, x.Year, x.Month, x.Score, scoreLabel = SatisfactionRatings.ScoreLabel(x.Score), x.Comment,
                x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt
            })
        };
    }
}
