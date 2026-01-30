using System.Diagnostics.CodeAnalysis;

namespace Dawn.MuMu.RichPresence.MuMu;

public class LogWatcher : IDisposable
{
    private readonly FileInfo _filePath;
    private FileSystemWatcher? _logFileWatcher;
    private readonly CancellationTokenSource _pokeCTS = new();

    [SuppressMessage("ReSharper", "RemoveRedundantBraces")]
    public LogWatcher(FileInfo filePath)
    {
        _filePath = filePath;
        if (filePath.Directory is { Exists: true })
        {
            CreateLogWatcher();
            return;
        }

        Log.Information("'{LogPath}' is not present, will wait for its creation via lazy initialization", filePath);

        Task.Factory.StartNew(async () =>
        {
            while (filePath.Directory is not { Exists: true })
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
        _logFileWatcher = new();
        _logFileWatcher.Path = _filePath.Directory?.FullName ?? ".";
        _logFileWatcher.Filter = _filePath.Name;
        _logFileWatcher.NotifyFilter = NotifyFilters.Size;
        _logFileWatcher.Changed += (_, args) => FileChanged?.Invoke(this, args);
        _logFileWatcher.Error += (_, args) => Error?.Invoke(this, args);

        _logFileWatcher.EnableRaisingEvents = _shouldRaiseEvents;

        Log.Verbose("MuMu logs are fully buffered. We need to poke the logs for the watcher to register an update, poking every second");
        Task.Run(async () =>
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (await timer.WaitForNextTickAsync(_pokeCTS.Token))
                _filePath.LastAccessTime = DateTime.Now;

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
            Log.Verbose("Watching for changes on file {FilePath}", _filePath.Name);
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
