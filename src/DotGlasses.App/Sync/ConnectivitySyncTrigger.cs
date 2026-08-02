using DotGlasses.App.Logging;
using DotGlasses.Contracts.Sync;
using Microsoft.JSInterop;

namespace DotGlasses.App.Sync;

/// <summary>
/// Starts draining the outbox (business data + batched client logs) on browser reconnect and
/// on a periodic timer as a fallback — Blazor WASM has no OS-level background task, so "detect
/// reconnect" here means the JS 'online' event plus a belt-and-braces poll.
/// </summary>
public class ConnectivitySyncTrigger(ISyncService syncService, BatchingLoggerProvider loggerProvider, IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private DotNetObjectReference<ConnectivitySyncTrigger>? _selfReference;

    public async Task StartAsync()
    {
        _selfReference = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.registerConnectivityCallback", _selfReference);
        _ = PollLoopAsync(_cts.Token);
    }

    [JSInvokable]
    public async Task OnOnline()
    {
        await loggerProvider.FlushAsync();
        await syncService.SyncPendingAsync();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await loggerProvider.FlushAsync();
            await syncService.SyncPendingAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        _selfReference?.Dispose();
    }
}
