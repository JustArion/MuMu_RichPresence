namespace Dawn.MuMu.RichPresence.PlayGames;

public class PlayGamesLogWatcher : IDisposable
{
    private FileSystemWatcher? _logFileWatcher;
    public PlayGamesLogWatcher(string filePath)
    {
        var fi = new FileInfo(filePath);
        if (fi.Directory is { Exists: true })
        {
            CreateLogWatcher(filePath);
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

            CreateLogWatcher(filePath);
        }, TaskCreationOptions.LongRunning);
    }

    private void CreateLogWatcher(string filePath)
    {
        _logFileWatcher = new();
        _logFileWatcher.Path = Path.GetDirectoryName(filePath)!;
        _logFileWatcher.Filter = Path.GetFileName(filePath);
        _logFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        _logFileWatcher.Changed += (_, args) => FileChanged?.Invoke(this, args);
        _logFileWatcher.Error += (_, args) => Error?.Invoke(this, args);

        _logFileWatcher.EnableRaisingEvents = _shouldRaiseEvents;
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

        Task.Run(onInitialize);
        _logFileWatcher.EnableRaisingEvents = true;
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

        if (_logFileWatcher == null)
            return;

        _logFileWatcher.EnableRaisingEvents = false;
        _logFileWatcher.Dispose();
    }
}
