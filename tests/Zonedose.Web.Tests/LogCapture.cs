using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Zonedose.Web.Tests;

/// <summary>Keeps every log line the application writes, to check that none carries personal data.</summary>
public sealed class LogCapture : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture capture, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            capture.Lines.Enqueue($"{category}: {formatter(state, exception)} {exception}");
    }
}
