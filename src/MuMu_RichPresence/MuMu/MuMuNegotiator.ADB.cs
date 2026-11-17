using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Dawn.MuMu.RichPresence.Extensions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.MuMu.Devices;
using Dawn.MuMu.RichPresence.MuMu.Interop;

namespace Dawn.MuMu.RichPresence.MuMu;

public static partial class MuMuNegotiator
{
    // Wait at least 15sec since emulator start (this is for ADB to boot up)
    private static async ValueTask WaitForADB(NxDevice device, CancellationToken token = default)
    {
        var deviceStartTime = DateTime.Now - device.UnderlyingProcess.StartTime;
        if (deviceStartTime < TimeSpan.FromSeconds(15))
        {
            Log.Verbose("Device is booting up");
            await Task.Delay(TimeSpan.FromSeconds(15) - deviceStartTime, token);
        }
    }

    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    private static void StartADB()
    {
        // When a new device is dispatched, we cancel the previous device's operations
        // We do this opposed to hoisting a PeriodicTimer since the FileWatchingOperation should also be able to be cancelled
        // So a CTS is better here imo
        CancellationTokenSource? cts = null;
        _deviceWatcher
            .SubscribeAsync(NewThreadScheduler.Default, async device =>
            {
                var ourCTS = new CancellationTokenSource();
                Interlocked.Exchange(ref cts, ourCTS)?
                    .Cancel();

                // We dispose the previous polling ahead of this check since if we don't the previous poll would continue
                // -- Emulator is closed
                if (device == null)
                    return;

                try
                {
                    await OnDeviceSwitched(device, ourCTS.Token);
                }
                catch (OperationCanceledException) when (ourCTS.IsCancellationRequested)
                {
                    // Expected: a newer device replaced this one. Not an error.
                }

            }, static e => Log.Error(e, "Couldn't execute ADB approach on new device"));
    }

    private static async Task OnDeviceSwitched(NxDevice device, CancellationToken token = default)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        await using var _ = token.Register(timer.Dispose);

        await WaitForADB(device, token);

        var interop = await MuMuInterop.TryCreate(device, keepAlive: true, token);
        if (interop == null)
        {
            Log.Error("MuMu Interop on {VM} could not be established for some reason, falling back to FileWatching", device.FindVM()?.Name ?? "Unknown Device");
            var fileWatchingOperation = StartFileWatching();
            token.Register(fileWatchingOperation.Dispose);
            return;
        }

        // ReSharper disable once MethodSupportsCancellation
        var disposable = Disposable.Create(interop, s => s.DisposeAsync().AsTask().Wait());
        _disposables.Add(disposable);
        try
        {
            // Will perf log once every time the device context is switched
            var once = false;
            // One of the few valid uses of do-while right here lol
            do
            {
                var sw = Stopwatch.GetTimestamp();
                var app = await interop.GetFocusedApp(token)
                    .Catch(e => Log.Error(e, "Exception while getting focused app"));

                var elapsed = Stopwatch.GetElapsedTime(sw);
                if (!once)
                {
                    Log.Verbose("Getting focused app took {Elapsed:F2} sec", elapsed.TotalSeconds);
                    once = true;
                }

                await DispatchFocusedApp(app);
            } while (await timer.WaitForNextTickAsync(token));
        }
        finally
        {
            _disposables.Remove(disposable);
            await interop.DisposeAsync();
        }
    }

    private static async Task DispatchFocusedApp(AndroidProcess? app)
    {
        if (app == null)
            await UpdatePresenceIfNecessary();

        // It's the same presence being updated
        if (app == null || (_focusedLifetime != null && app.AppInfo.PackageName == _focusedLifetime.PackageName))
            return;

        var lifetime = CreateLifetimeFromProcess(app);
        await UpdatePresenceIfNecessary(lifetime);
    }

    private static MuMuSessionLifetime CreateLifetimeFromProcess(AndroidProcess app)
    {
        var session = new MuMuSessionLifetime
        {
            AppState = AppState.Started,
            PackageName = app.AppInfo.PackageName,
            Title = app.Title,
            StartTime = app.AppInfo.StartTime
        };
        session.AppState.Value = AppState.Focused;
        return session;
    }
}
