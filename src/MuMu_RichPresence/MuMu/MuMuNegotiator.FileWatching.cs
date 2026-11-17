using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Dawn.MuMu.RichPresence.Extensions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence.MuMu;

public static partial class MuMuNegotiator
{
    // The tray uses this to check if a button should be shown to open the respective log file
    public static BehaviorSubject<FileInfo?> LogFileChanged { get; } = new(null);

    private static MuMuPlayerLogReader? _logReader;

    private static IDisposable StartFileWatching()
    {
        var disposable = _deviceWatcher.CurrentDevice
            .ObserveOn(TaskPoolScheduler.Default)
            .SubscribeGuarded(device =>
            {
                // When the emulator closes and there's no other emulators present, then we stop reading
                if (device == null)
                {
                    Interlocked.Exchange(ref _logReader, null)?
                        .Dispose();
                    return;
                }

                var filePath = device.FindLogFile();
                if (filePath == null)
                {
                    Log.Debug("Couldn't find a log file associated with {ProcessName}<{ProcessId}>", device.UnderlyingProcess.ProcessName, device.UnderlyingProcess.Id);
                    return;
                }

                LogFileChanged.OnNext(filePath);

                // Stop the previous read operation if any and create a new one
                var reader = new MuMuPlayerLogReader(device);
                Interlocked.Exchange(ref _logReader, reader)?
                    .Dispose();

                var sessions = reader.Sessions;

                // TODO: Check if there's some redundant things happening here
                // The else on focused seems redundant, but will need to check
                sessions.ItemsAdded
                    .WhereSafe(lt => !AppLifetimeParser.IsSystemLevelPackage(lt.PackageName))
                    .SelectManySafe(lt => lt.AppState
                            .WhenPropertyChanged(x => x.Value)
                            .Select(x => (Lifetime: lt, State: x.Value)))
                    .Do(static x => Log.Debug("Updating from AppState Change {NewState}", x.State))
                    .SubscribeAsync(x => x.State == AppState.Focused
                            ? UpdatePresenceIfNecessary(x.Lifetime)
                            : UpdatePresenceIfNecessary(reader.GetFocusedApp()),
                        static e => Log.Error(e, "Presence update failed"));

                sessions.ItemsRemoved
                    .Do(static lifetime => Log.Verbose("Session updated: {Lifetime}", lifetime.Title))
                    .SubscribeAsync(async _ => await UpdatePresenceIfNecessary(reader.GetFocusedApp()),
                        static e => Log.Error(e, "Presence update failed"));

                reader.StartAsync(); // This starts a long running operation, the method doesn't need to be awaited
            }, static e => Log.Error(e, "New Device File Watching failed"));

        return Disposable.Create(disposable, static disp =>
        {
            disp.Dispose();
            Interlocked.Exchange(ref _logReader, null)?
                .Dispose();
        });
    }
}
