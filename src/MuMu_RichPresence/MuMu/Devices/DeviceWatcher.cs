using System.Diagnostics;
using System.Reactive.Subjects;
using System.Runtime.Versioning;
using Dawn.MuMu.RichPresence.Tools;
using Vanara.PInvoke;

namespace Dawn.MuMu.RichPresence.MuMu.Devices;

public class DeviceWatcher : IDisposable, IObservable<NxDevice?>
{
    public BehaviorSubject<NxDevice?> CurrentDevice { get; }
    private readonly PeriodicTimer _timer;

    public DeviceWatcher()
    {
        CurrentDevice = new(null);

        _timer = new(TimeSpan.FromSeconds(1));
        Task.Factory.StartNew(static async state =>
        {
            var @this = (DeviceWatcher)state!;
            var timer = @this._timer;

            while (await timer.WaitForNextTickAsync())
            {
                try
                {
                    @this.PollUpdate();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception while polling");
                }
            }
        }, this, TaskCreationOptions.LongRunning);
    }

    // We walk through all the visible windows and find the first that's an NxDevice window, then set that as our current NxDevice
    // This approach works to find the last used emulator instance, then to display their rich presence
    // We're using this for the posibility that a user's running multiple emulators, across multiple Android versions at the same time
    [SupportedOSPlatform("windows")]
    private void PollUpdate()
    {
        var devices = Process.GetProcessesByName(Pathfinder.NxDevice);
        if (devices.Length == 0)
        {
            if (!_disposed && CurrentDevice is { IsDisposed: false, Value: not null })
                DispatchUpdate(null);
            return;
        }

        for (var hwnd = User32.GetTopWindow(); !hwnd.IsNull; hwnd = User32.GetWindow(hwnd, User32.GetWindowCmd.GW_HWNDNEXT))
        {
            if (hwnd.IsNull)
                return; // No windows available

            _ = User32.GetWindowThreadProcessId(hwnd, out var pid);

            if (devices.FirstOrDefault(x => x.Id == pid) is not { } proc)
                continue;

            if (proc.Id == CurrentDevice.Value?.UnderlyingProcess.Id)
                return;

            if (!(_disposed || CurrentDevice.IsDisposed))
                DispatchUpdate(NxDevice.CreateFromProcess(proc));
            return;
        }
    }

    private void DispatchUpdate(NxDevice? device)
    {
        Log.Verbose("NxDevice Changed -> {Device}", device == null
            ? "None"
            : device.FindVM() is { } vm
                ? vm.Name
                : device.File.FullName);

        CurrentDevice.OnNext(device);
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        GC.SuppressFinalize(this);
        _timer.Dispose();
        CurrentDevice.Dispose();
    }

    public IDisposable Subscribe(IObserver<NxDevice?> observer) => CurrentDevice.Subscribe(observer);
}
