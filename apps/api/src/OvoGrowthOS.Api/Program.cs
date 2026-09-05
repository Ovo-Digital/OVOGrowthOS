using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, logger) => logger.ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services).Enrich.FromLogContext().WriteTo.Console());
builder.Services.AddProblemDetails();
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
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromMinutes(1),
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), RoleClaimType = System.Security.Claims.ClaimTypes.Role };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ReadAccess", p => p.RequireRole("Admin", "Partner", "Analyst"))
    .AddPolicy("EvaluationWrite", p => p.RequireRole("Admin", "Partner", "Analyst"))
    .AddPolicy("OperationsWrite", p => p.RequireRole("Admin", "Partner"))
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

var app = builder.Build();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapPost("/api/auth/login", (LoginRequest request, IConfiguration config, JwtTokenService tokens) =>
{
    var email = config["DefaultAdmin:Email"]!; var hash = config["DefaultAdmin:PasswordHash"]!;
    return request.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && JwtTokenService.VerifyPassword(request.Password, hash)
        ? Results.Ok(new { token = tokens.Create(email, "Admin"), expiresAt = DateTimeOffset.UtcNow.AddHours(8), user = new { email, name = "OVO Admin", role = "Admin" } })
        : Results.Problem(statusCode: 401, title: "E-posta adresi veya şifre hatalı");
}).AllowAnonymous();

app.MapWorkflowEndpoints();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}
app.Run();

public sealed record LoginRequest(string Email, string Password);
public partial class Program;
