using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Dawn.MuMu.RichPresence.Extensions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence.MuMu;

public static partial class MuMuNegotiator
{
    private static IDisposable StartFileWatching()
    {
        return _deviceWatcher.CurrentDevice
            .ObserveOn(new NewThreadScheduler())
            .Subscribe(device =>
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
                Interlocked.Exchange(ref _logReader, new MuMuPlayerLogReader(device))?
                    .Dispose();

                var reader = _logReader;
                var sessions = reader.Sessions;

                // TODO: Check if there's some redundant things happening here
                // The else on focused seems redundant, but will need to check
                sessions.ItemsAdded.Where(x => !AppLifetimeParser.IsSystemLevelPackage(x.PackageName))
                    .Subscribe(lt => lt.AppState
                        .WhenPropertyChanged(x => x.Value)
                        .Select(x => x.Value)
                        .Subscribe(state =>
                        {
                            if (state == AppState.Focused)
                                Task.Run(() => UpdatePresenceIfNecessary(lt));
                            else
                                Task.Run(() => UpdatePresenceIfNecessary(reader.GetFocusedApp()));

                            Log.Debug("Updating from AppState Change {NewState}", state);
                        }));
                sessions.ItemsRemoved.Subscribe(lifetime =>
                {
                    Log.Verbose("Session updated: {Lifetime}", lifetime.Title);
                    Task.Run(() => UpdatePresenceIfNecessary(reader.GetFocusedApp()));
                });

                reader.StartAsync(); // This starts a long running operation, the method doesn't need to be awaited
            });
    }

    // The tray uses this to check if a button should be shown to open the respective log file
    public static BehaviorSubject<FileInfo?> LogFileChanged { get; } = new(null);
}
