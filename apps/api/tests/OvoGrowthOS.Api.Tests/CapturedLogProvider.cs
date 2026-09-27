using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OvoGrowthOS.Api.Tests;

// Test sırasında ILogger üzerinden yazılan satırları toplar (Serilog konsol hattı değil).
public sealed class CapturedLogProvider(ConcurrentQueue<string> lines) : ILoggerProvider
{
    internal readonly ConcurrentQueue<string> Lines = lines;
    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
    public void Dispose() { }

    private sealed class Logger(CapturedLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => owner.Lines.Enqueue($"{logLevel}|{category}|{formatter(state, exception)}");
    }
}
