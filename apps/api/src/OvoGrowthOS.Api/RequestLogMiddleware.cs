using System.Diagnostics;

namespace OvoGrowthOS.Api;

// Sadece 5xx ve eşik üstü istekleri uyarıyla kaydeder. Sorgu dizesi, başlık, gövde ve çerez yazılmaz.
public sealed class RequestLogMiddleware(RequestDelegate next, ILogger<RequestLogMiddleware> logger, IConfiguration configuration)
{
    private readonly int _slowMs = int.TryParse(configuration["RequestLog:SlowMs"], out var parsed) ? parsed : 1000;

    public async Task InvokeAsync(HttpContext http)
    {
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try
        {
            await next(http);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            var durationMs = (long)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            var status = failure is null ? http.Response.StatusCode : 500;
            if (status >= 500 || durationMs >= _slowMs)
                logger.LogWarning("Yavaş veya hatalı istek {RequestId} {Method} {Path} {StatusCode} {DurationMs} {UserId} {Role}",
                    http.TraceIdentifier, http.Request.Method, http.Request.Path.Value, status, durationMs,
                    http.User.FindFirst("uid")?.Value, http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
        }
    }
}
