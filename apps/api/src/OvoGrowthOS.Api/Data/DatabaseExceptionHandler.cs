using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OvoGrowthOS.Api.Data;

public sealed class DatabaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        string? message = exception switch
        {
            DbUpdateConcurrencyException => "Kayıt başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.TableName is "PortalAccesses" or "PortalReports" or "PortalDocumentShares" => "Portal kaydı zaten oluşturulmuş veya başka bir işlemle değişmiş. Sayfayı yenileyip kontrol edin.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.TableName is "ServiceCostAccounts" or "ServiceCostEntries" or "InvestmentAccounts" or "InvestmentEntries" => "Maliyet veya yatırım referansı zaten kayıtlı. Sayfayı yenileyip kontrol edin; ikinci kez eklenmedi.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.TableName is "CollectionAccounts" or "CollectionPayments" => "Fatura veya ödeme referansı zaten kayıtlı. Sayfayı yenileyin; aynı ödeme ikinci kez eklenmedi.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.TableName is "WorkTasks" or "BrandFollowUps" or "BrandContactNotes" => "Görev veya takip kaydı daha önce kaydedilmiş. Listeyi yenileyin; aynı kayıt ikinci kez oluşturulmadı.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.ConstraintName == "IX_UserAccounts_Email" => "Bu e-posta ile kayıtlı bir kullanıcı zaten var.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.ConstraintName == "IX_MonthlyPerformances_BrandId_Year_Month" => "Bu marka ve dönem için daha önce kayıt oluşturulmuş.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                when postgres.ConstraintName == "IX_PartnershipDeals_BrandId" => "Bu markanın zaten etkin bir anlaşması var.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } => "Veriler finansal güvenlik kurallarını karşılamıyor.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } => "İlişkili kayıt bulunamadığı için işlem tamamlanamadı.",
            _ => null
        };
        if (message is null) return false;
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = message }, cancellationToken);
        return true;
    }
}
