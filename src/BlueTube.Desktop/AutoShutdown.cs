using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BlueTube.Desktop;

/// <summary>
/// Stops the app when no BlueTube window is connected for <see cref="Grace"/>.
/// Counts connections, not circuits: Blazor keeps a closed window's circuit for about 3 min
/// (in case it reconnects), so OnCircuitClosed comes too late. OnConnectionDown comes at once.
/// A page reload drops and opens a connection; the grace time covers that.
/// </summary>
public sealed class AutoShutdown(IHostApplicationLifetime lifetime) : CircuitHandler
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private int _connected;
    private CancellationTokenSource? _pending;

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
    {
        Interlocked.Increment(ref _connected);
        Interlocked.Exchange(ref _pending, null)?.Cancel();
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
    {
        if (Interlocked.Decrement(ref _connected) > 0) return Task.CompletedTask;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _pending, cts)?.Cancel();
        _ = Task.Delay(Grace, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled && Volatile.Read(ref _connected) == 0) lifetime.StopApplication();
        }, TaskScheduler.Default);
        return Task.CompletedTask;
    }
}
