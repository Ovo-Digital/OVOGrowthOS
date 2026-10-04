using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OvoGrowthOS.Api.Tests;

public sealed class RequestLoggingTests
{
    private sealed class CapturingLogger : ILogger<RequestLogMiddleware>
    {
        public readonly List<string> Lines = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add($"{logLevel}|{formatter(state, exception)}");
    }

    private static IConfiguration Config(string slowMs) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["RequestLog:SlowMs"] = slowMs }).Build();

    private static DefaultHttpContext Context(string path, string query)
    {
        var http = new DefaultHttpContext();
        http.TraceIdentifier = "istek-kimligi-123";
        http.Request.Method = "GET";
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);
        http.Request.Headers["Authorization"] = "Bearer gizli-baslik-degeri";
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("uid", "33333333-3333-3333-3333-333333333333"), new Claim(ClaimTypes.Role, "Analyst")], "Test"));
        return http;
    }

    [Fact]
    public async Task Failed_request_is_logged_with_operation_fields_only()
    {
        var logger = new CapturingLogger();
        var http = Context("/api/performance", "?secret=gizli-sorgu-degeri");
        var middleware = new RequestLogMiddleware(_ => { http.Response.StatusCode = 503; return Task.CompletedTask; }, logger, Config("1000000"));
        await middleware.InvokeAsync(http);

        var line = Assert.Single(logger.Lines);
        Assert.StartsWith("Warning|", line);
        foreach (var expected in new[] { "istek-kimligi-123", "GET", "/api/performance", "503", "33333333-3333-3333-3333-333333333333", "Analyst" })
            Assert.Contains(expected, line);
        foreach (var secret in new[] { "gizli-sorgu-degeri", "gizli-baslik-degeri", "Authorization" })
            Assert.DoesNotContain(secret, line);
    }

    [Fact]
    public async Task Thrown_exception_is_logged_as_server_error()
    {
        var logger = new CapturingLogger();
        var http = Context("/api/evaluations", "");
        var middleware = new RequestLogMiddleware(_ => throw new InvalidOperationException("dahili"), logger, Config("1000000"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(http));

        var line = Assert.Single(logger.Lines);
        Assert.Contains("500", line);
        Assert.DoesNotContain("dahili", line);
    }

    [Fact]
    public async Task Slow_request_is_logged_and_fast_successful_request_is_not()
    {
        var slowLogger = new CapturingLogger();
        var slowHttp = Context("/api/lead-follow-ups", "");
        var slow = new RequestLogMiddleware(async _ => { await Task.Delay(30); slowHttp.Response.StatusCode = 200; }, slowLogger, Config("10"));
        await slow.InvokeAsync(slowHttp);
        Assert.Single(slowLogger.Lines);

        var fastLogger = new CapturingLogger();
        var fastHttp = Context("/api/notifications", "");
        var fast = new RequestLogMiddleware(_ => { fastHttp.Response.StatusCode = 200; return Task.CompletedTask; }, fastLogger, Config("1000000"));
        await fast.InvokeAsync(fastHttp);
        Assert.Empty(fastLogger.Lines);
    }

    [Fact]
    public async Task Live_requests_never_write_credentials_tokens_or_query_values_into_logs()
    {
        var secrets = new List<string>();
        var original = Console.Out;
        var output = new LockedWriter();
        Console.SetOut(output);
        try
        {
            await using var factory = new WorkflowApiFactory();
            factory.ConfigurationOverrides["RequestLog:SlowMs"] = "0";
            var client = factory.CreateClient();

            var denied = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = "Gizli-Deneme-Parola-77" });
            Assert.False(denied.IsSuccessStatusCode);

            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            secrets.Add(token);

            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var health = await client.GetAsync("/health?gizli=sorgu-degeri-9911");
            health.EnsureSuccessStatusCode();
            // Console sink flushes asynchronously; under parallel load a fixed delay flakes.
            // Poll for the expected line instead of assuming it arrives within 50 ms.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!output.Text.Contains("/health") && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50);
        }
        finally
        {
            Console.SetOut(original);
        }

        var log = output.Text;
        Assert.Contains("Yavaş veya hatalı istek", log);
        Assert.Contains("/api/auth/login", log);
        Assert.Contains("/health", log);
        secrets.Add("Gizli-Deneme-Parola-77");
        secrets.Add(WorkflowApiFactory.TestPassword);
        secrets.Add("sorgu-degeri-9911");
        foreach (var secret in secrets) Assert.DoesNotContain(secret, log);
    }

    private sealed class LockedWriter : TextWriter
    {
        private readonly StringBuilder _buffer = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) { lock (_buffer) _buffer.Append(value); }
        public override void Write(string? value) { if (value is null) return; lock (_buffer) _buffer.Append(value); }
        public string Text { get { lock (_buffer) return _buffer.ToString(); } }
    }
}
