using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Validation;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, logger) => logger.ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services).Enrich.FromLogContext().WriteTo.Console());
builder.Services.AddProblemDetails();
builder.Services.AddDataProtection();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Database"),
    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "growth")));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Çok sayıda giriş denemesi yapıldı. Bir dakika bekleyip yeniden deneyin." }, cancellationToken);
    };
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromMinutes(1),
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), RoleClaimType = System.Security.Claims.ClaimTypes.Role };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            if (!Guid.TryParse(principal.FindFirstValue("uid"), out var id) ||
                !int.TryParse(principal.FindFirstValue("session_version"), out var version))
            {
                context.Fail("Oturumunuz sona erdi. Yeniden giriş yapın.");
                return;
            }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
            if (account is null || !account.IsActive || account.TokenVersion != version ||
                account.Role != principal.FindFirstValue(ClaimTypes.Role) || account.Email != principal.FindFirstValue(ClaimTypes.Email))
                context.Fail("Hesabınız veya yetkiniz değişti. Yeniden giriş yapın.");
            else if (account.Role == "BrandClient" && !await db.PortalAccesses.AnyAsync(x => x.UserId == id, context.HttpContext.RequestAborted))
                context.Fail("Marka erişiminiz bulunamadı.");
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ReadAccess", p => p.RequireRole("Admin", "Partner", "Analyst"))
    .AddPolicy("EvaluationWrite", p => p.RequireRole("Admin", "Partner", "Analyst"))
    .AddPolicy("OperationsWrite", p => p.RequireRole("Admin", "Partner"))
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PortalAccess", p => p.RequireRole("BrandClient"))
    .AddPolicy("SessionAccess", p => p.RequireRole("Admin", "Partner", "Analyst", "BrandClient"));

var app = builder.Build();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/portal") || context.Request.Path.StartsWithSegments("/api/portal-management"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
    await next(context);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapPost("/api/auth/login", async (LoginRequest request, JwtTokenService tokens, AppDbContext db) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email && x.IsActive);
    var validPassword = JwtTokenService.VerifyPassword(request.Password, account?.PasswordHash ?? JwtTokenService.DummyPasswordHash);
    var allowed = account is not null && (account.Role is ("Admin" or "Partner" or "Analyst") ||
        account.Role == "BrandClient" && await db.PortalAccesses.AnyAsync(x => x.UserId == account.Id));
    return validPassword && allowed && account is not null
        ? Results.Ok(new { token = tokens.Create(account), expiresAt = DateTimeOffset.UtcNow.AddHours(8), user = new { account.Id, account.Email, account.Name, account.Role } })
        : Results.Problem(statusCode: 401, title: "E-posta adresi veya şifre hatalı");
}).AddEndpointFilter<ValidationFilter<LoginRequest>>().RequireRateLimiting("login").AllowAnonymous();
app.MapGet("/api/auth/me", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var id = Guid.Parse(user.FindFirstValue("uid")!);
    return Results.Ok(await db.UserAccounts.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new { x.Id, x.Email, x.Name, x.Role }).SingleAsync());
}).RequireAuthorization("SessionAccess");

app.MapWorkflowEndpoints();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<IConfiguration>());
}
app.Run();

public sealed record LoginRequest(string Email, string Password);
public partial class Program;
