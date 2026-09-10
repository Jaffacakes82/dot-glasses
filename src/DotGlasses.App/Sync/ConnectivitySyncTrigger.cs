using DotGlasses.App.Logging;
using DotGlasses.Contracts.Sync;
using Microsoft.JSInterop;

namespace DotGlasses.App.Sync;

/// <summary>
/// Starts draining the outbox (business data + batched client logs) on browser reconnect and
/// on a periodic timer as a fallback — Blazor WASM has no OS-level background task, so "detect
/// reconnect" here means the JS 'online'/'offline' events plus a belt-and-braces poll.
///
/// Also the single source of truth for *displayed* connectivity status: ConnectivityChanged fires
/// on both browser events and every poll tick, so a subscriber (Home.razor's banner) reflects a
/// real connectivity change within one poll interval even if the browser never fires 'offline' in
/// time — before this, nothing ever re-checked status for display, only for triggering a sync.
/// </summary>
public class ConnectivitySyncTrigger(ISyncService syncService, BatchingLoggerProvider loggerProvider, IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private DotNetObjectReference<ConnectivitySyncTrigger>? _selfReference;

    /// <summary>Raised after a browser online/offline event or a poll tick — never carries the new
    /// state itself, so a subscriber re-reads it (e.g. via dotGlassesIdb.isOnline()) rather than
    /// trusting which event fired, since a poll tick means "check now," not "you're online."</summary>
    public event Action? ConnectivityChanged;

    public async Task StartAsync()
    {
        _selfReference = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.registerConnectivityCallback", _selfReference);
        _ = PollLoopAsync(_cts.Token);
    }

    [JSInvokable]
    public async Task OnOnline()
    {
        ConnectivityChanged?.Invoke();
        await loggerProvider.FlushAsync();
        await syncService.SyncPendingAsync();
    }

    [JSInvokable]
    public Task OnOffline()
    {
        ConnectivityChanged?.Invoke();
        return Task.CompletedTask;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            ConnectivityChanged?.Invoke();
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
