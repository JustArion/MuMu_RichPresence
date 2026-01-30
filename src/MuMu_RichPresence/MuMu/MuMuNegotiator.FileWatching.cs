using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive.Subjects;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence.MuMu;

public static partial class MuMuNegotiator
{
    private static void StartFileWatching()
    {
        // The below code is within a Task block since 'GetOrWaitForFilePath' can take an unknown amount of time to complete
        Task.Run(async () =>
        {
            var filePath = await Pathfinder.GetOrWaitForLogFilePath();
            LogSubject.OnNext(filePath);

            _logReader = new MuMuPlayerLogReader(filePath, _currentProcessState);
            _logReader.Sessions.CollectionChanged += ReaderSessionsChanged;
            _logReader.StartAsync(); // This starts a long running operation, the method doesn't need to be awaited
        });
    }

    private static void ReaderSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
            return; // We don't care about reset events
        try
        {
            Log.Debug("[{ChangeType}] Session Updated {PossibleNewItems}", e.Action, e.NewItems);

            var logReader = _logReader!;
            if (e.Action != NotifyCollectionChangedAction.Add)
                Task.Run(() => UpdatePresenceIfNecessary(logReader.GetFocusedApp()));
            else
            {
                if (e.NewItems == null)
                    return;

                foreach (var lifetime in e.NewItems.Cast<MuMuSessionLifetime>())
                {
                    if (AppLifetimeParser.IsSystemLevelPackage(lifetime.PackageName))
                        continue;

                    lifetime.AppState.WhenPropertyChanged(entry => entry.Value).Subscribe(a =>
                    {

                        if (a.Value == AppState.Focused)
                            Task.Run(() => UpdatePresenceIfNecessary(lifetime));
                        else
                            Task.Run(() => UpdatePresenceIfNecessary(logReader.GetFocusedApp()));

                        Log.Debug("Updating from AppState Change {NewState}", a.Value);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while handling session collection change");
        }
    }

    public static BehaviorSubject<FileInfo?> LogSubject { get; } = new(null);
}
