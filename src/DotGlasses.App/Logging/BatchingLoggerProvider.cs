using System.Collections.Concurrent;
using System.Text.Json;
using DotGlasses.Contracts.ClientLogs;
using DotGlasses.Contracts.Sync;

namespace DotGlasses.App.Logging;

/// <summary>
/// Batches structured log entries (warnings, errors, key lifecycle events) and ships them
/// through the same offline outbox as business data — errors are exactly as likely to happen
/// offline as online, and nobody is watching the browser console once the app is on a field
/// agent's phone (brief 3.5a). Doesn't replace the default console provider, just adds to it.
/// </summary>
public class BatchingLoggerProvider(ISyncQueueStore queueStore, IClientSessionContext sessionContext) : ILoggerProvider
{
    private const int FlushThreshold = 20;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentQueue<ClientLogEntryDto> _buffer = new();

    public ILogger CreateLogger(string categoryName) => new BatchingLogger(categoryName, this);

    internal void Enqueue(ClientLogEntryDto entry)
    {
        _buffer.Enqueue(entry);
        if (_buffer.Count >= FlushThreshold)
        {
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        List<ClientLogEntryDto> entries = [];
        while (_buffer.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            return;
        }

        var batch = new ClientLogBatchDto { CorrelationId = sessionContext.SessionCorrelationId, Entries = entries };
        var item = new OutboxItem
        {
            Id = Guid.NewGuid(),
            EntityType = "ClientLogBatch",
            ApiRoute = "api/v1/client-logs",
            PayloadJson = JsonSerializer.Serialize(batch, JsonOptions),
        };

        await queueStore.EnqueueAsync(item);
    }

    public void Dispose()
    {
    }

    private sealed class BatchingLogger(string categoryName, BatchingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Enqueue(new ClientLogEntryDto
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = logLevel.ToString(),
                Category = categoryName,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
            });
        }
    }
}
