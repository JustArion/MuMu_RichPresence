using System.Diagnostics;
using System.Management;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Dawn.MuMu.RichPresence.Extensions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.MuMu.Interop;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu;

public static partial class MuMuNegotiator
{
    private const string NxDevice = "MuMuNxDevice";
    private static CancellationTokenSource _adbCTS = new();
    private static void StartADB()
    {
        // It's important that we create a closure here, we need a reference to the current cts
        _disposables.Add(Disposable.Create(()=> _adbCTS.Cancel()));

        var monitor = new ProcessMonitor($"{NxDevice}.exe");
        monitor.Started
            .Subscribe(EmulatorStarted);
        monitor.Closed.Subscribe(EmulatorClosed);

        if (Process.GetProcessesByName(NxDevice).Length != 0)
            EmulatorStarted(Unit.Default);
    }

    private static void EmulatorClosed(Unit _)
    {
        _adbCTS.Cancel();
        _adbCTS.Dispose();
        _adbCTS = new();
        Log.Debug("Emulator closed");
    }

    private static void EmulatorStarted(Unit _)
    {
        Log.Debug("The emulator is running");
        var token = _adbCTS.Token;
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        token.Register(() => timer.Dispose());

        Task.Factory.StartNew(PollADB, timer, TaskCreationOptions.LongRunning);
    }

    private static async Task PollADB(object? boxedTimer)
    {
        var timer = (PeriodicTimer)boxedTimer!;
        var interop = await MuMuInterop.TryCreate(keepAlive: true);

        if (interop == null)
        {
            Log.Error("MuMu Interop could not be established for some reason, falling back to FileWatching");
            throw new NotImplementedException();
        }

        // Wait at least 15sec since emulator start (this is for ADB to boot up)
        var timeSinceEmulatorStart = DateTime.Now - Process.GetProcessesByName(NxDevice).First().StartTime;
        if (timeSinceEmulatorStart < TimeSpan.FromSeconds(15))
            await Task.Delay(TimeSpan.FromSeconds(15) - timeSinceEmulatorStart);

        var disposable = Disposable.Create(interop, s => s.DisposeAsync().AsTask().Wait());
        _disposables.Add(disposable);
        try
        {
            // One of the few valid uses of do-while right here xD
            do
            {
                var app = await interop.GetFocusedApp()
                    .Catch(e => Log.Error(e, "Exception while getting focused app"));
                if (app == null)
                    await UpdatePresenceIfNecessary();

                if (app == null || (_focusedLifetime != null && app.AppInfo.PackageName == _focusedLifetime.PackageName))
                    continue;

                var lifetime = CreateLifetimeFromProcess(app);
                await UpdatePresenceIfNecessary(lifetime);
            } while (await timer.WaitForNextTickAsync());
        }
        finally
        {
            _disposables.Remove(disposable);
            await interop.DisposeAsync();
        }
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
