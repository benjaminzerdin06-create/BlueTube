using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BlueTube.Desktop;

/// <summary>
/// Stops the app when no BlueTube window is open for <see cref="Grace"/>.
/// A circuit is one open window/tab. A page reload closes and opens a circuit; the grace time covers that.
/// </summary>
public sealed class AutoShutdown(IHostApplicationLifetime lifetime) : CircuitHandler
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private int _open;
    private CancellationTokenSource? _pending;

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        Interlocked.Increment(ref _open);
        Interlocked.Exchange(ref _pending, null)?.Cancel();
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        if (Interlocked.Decrement(ref _open) > 0) return Task.CompletedTask;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _pending, cts)?.Cancel();
        _ = Task.Delay(Grace, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled && Volatile.Read(ref _open) == 0) lifetime.StopApplication();
        }, TaskScheduler.Default);
        return Task.CompletedTask;
    }
}
