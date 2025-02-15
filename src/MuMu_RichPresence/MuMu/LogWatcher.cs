using System.Diagnostics.CodeAnalysis;

namespace Dawn.MuMu.RichPresence;

public class LogWatcher : IDisposable
{
    private readonly string _filePath;
    private FileSystemWatcher? _logFileWatcher;
    private CancellationTokenSource _pokeCTS = new();

    [SuppressMessage("ReSharper", "RemoveRedundantBraces")]
    public LogWatcher(string filePath)
    {
        _filePath = filePath;
        var fi = new FileInfo(filePath);
        if (fi.Directory is { Exists: true })
        {
            CreateLogWatcher();
            return;
        }

        Log.Information("'{LogPath}' is not present, will wait for its creation via lazy initialization", filePath);

        Task.Factory.StartNew(async () =>
        {
            while (fi.Directory is not { Exists: true })
                await Task.Delay(TimeSpan.FromMinutes(1));

            if (_initializationSubscription != null)
            {
                try
                {
                    await _initializationSubscription();

                }
                catch (Exception e)
                {
                    Log.Error(e, "An error occurred during initialization");
                }

            }
            Log.Information("'{LogPath}' is now present, will start watching", filePath);

            CreateLogWatcher();
        }, TaskCreationOptions.LongRunning);
    }

    private void CreateLogWatcher()
    {
        var file = new FileInfo(_filePath);

        _logFileWatcher = new();
        _logFileWatcher.Path = file.Directory?.FullName ?? ".";
        _logFileWatcher.Filter = file.Name;
        _logFileWatcher.NotifyFilter = NotifyFilters.Size;
        _logFileWatcher.Changed += (_, args) => FileChanged?.Invoke(this, args);
        _logFileWatcher.Error += (_, args) => Error?.Invoke(this, args);

        _logFileWatcher.EnableRaisingEvents = _shouldRaiseEvents;

        Log.Verbose("Log Watcher created!");

        Log.Verbose("MuMu logs are fully buffered. We need to poke the logs for the watcher to register an update, poking every 100ms");
        Task.Run(async () =>
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

            while (await timer.WaitForNextTickAsync(_pokeCTS.Token))
                file.LastAccessTime = DateTime.Now;

        }, _pokeCTS.Token);
    }

    public event EventHandler<FileSystemEventArgs>? FileChanged;
    public event EventHandler<ErrorEventArgs>? Error;

    private bool _shouldRaiseEvents;
    private Func<Task>? _initializationSubscription;

    public void Initialize(Func<Task> onInitialize)
    {
        if (_logFileWatcher is null)
        {
            _initializationSubscription = onInitialize;
            _shouldRaiseEvents = true;
            return;
        }

        Task.Run(onInitialize).ContinueWith(_ =>
        {
            _logFileWatcher.EnableRaisingEvents = true;
            Log.Information("Watching for changes on file {FilePath}", _filePath);
        });
    }

    public void Stop()
    {
        if (_logFileWatcher is null)
        {
            _shouldRaiseEvents = false;
            return;
        }

        _logFileWatcher.EnableRaisingEvents = false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _pokeCTS.Cancel();

        if (_logFileWatcher == null)
            return;

        _logFileWatcher.EnableRaisingEvents = false;
        _logFileWatcher.Dispose();
    }
}
