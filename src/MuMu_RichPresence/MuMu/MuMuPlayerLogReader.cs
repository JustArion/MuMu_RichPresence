// #define LOG_APP_SESSION_MESSAGES

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dawn.MuMu.RichPresence.Logging.Serilog;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu;

public class MuMuPlayerLogReader(string filePath) : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<MuMuPlayerLogReader>();

    private bool _started;
    private long _lastStreamPosition;
    private readonly LogWatcher _logWatcher = new(filePath);
    public ObservableCollection<MuMuSessionLifetime> Sessions { get; } = [];
    public ObservableCollection<MuMuSessionLifetime> SessionGraveyard { get; } = [];

    public void StartAsync()
    {
        if (_started)
            return;
        _started = true;

        Task.Factory.StartNew(InitiateWatchOperation, TaskCreationOptions.LongRunning);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FileLock AquireFileLock() => FileLock.Aquire(filePath);

    private async Task InitiateWatchOperation()
    {
        Log.Verbose("Doing fresh read-operation pass on file {Path}", filePath);

        // Wait till the file exists
        if (!File.Exists(filePath))
        {
            _logger.Debug("File not found: Service.log");
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        _logWatcher.Error += LogFileWatcherOnError;
        _logWatcher.FileChanged += LogFileWatcherOnFileChanged;
        _logWatcher.Initialize(async () =>
        {
            _reading = true;
            try
            {
                if (File.Exists(filePath))
                {
                    await using var fileLock = AquireFileLock();
                    await CatchUpAsync(fileLock);
                }
            }
            finally
            {
                _reading = false;
            }
        });
    }

    private async Task CatchUpAsync(FileLock fileLock)
    {
        try
        {
            var ts = Stopwatch.GetTimestamp();
            var reader = fileLock.Reader;
            var sessions = new ObservableCollection<MuMuSessionLifetime>();
            // We read the old entries (To check if there's a game currently running)
            using (Warn.OnLongerThan(TimeSpan.FromSeconds(2), "Catch-Up took unusually long"))
                await GetAllSessionInfos(fileLock, sessions, SessionGraveyard);
            _lastStreamPosition = reader.BaseStream.Position;

            var processedEvents = sessions.Select(x => x?.PackageLifetimeEntries?.Count ?? 0).Sum() +
                                  SessionGraveyard.Select(x => x?.PackageLifetimeEntries?.Count ?? 0).Sum();

            Sessions.Clear();
            foreach (var session in sessions)
                Sessions.Add(session);

            // Emitting means that there's a game currently running AFTER we started, meaning we flag it for the Rich Presence to trigger
            if (sessions.FirstOrDefault(x => x.AppState.Value is AppState.Focused or AppState.Started) is { } lifetime)
                Log.Verbose(
                    "Caught up in {ExecutionDuration:F}ms (Processed {EventsProcessed} events), emitting {SessionInfo}",
                    Stopwatch.GetElapsedTime(ts).TotalMilliseconds, processedEvents, lifetime);
            else
                Log.Verbose(
                    "Caught up in {ExecutionDuration:F}ms, no games are currently running (Processed {EventsProcessed} events)",
                    Stopwatch.GetElapsedTime(ts).TotalMilliseconds, sessions.Count);

            Log.Debug("CatchUp: Read {Lines} lines ({FileSize} mb)[{Position}]", _initialLinesRead,
                Math.Round(reader.BaseStream.Length / Math.Pow(1024, 2), 0), reader.BaseStream.Position);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to read the file {FilePath}", filePath);
        }
    }

    private void LogFileWatcherOnError(object? _, ErrorEventArgs e) => _logger.Error(e.GetException(), "File watcher error");

    private bool _reading;
    private void LogFileWatcherOnFileChanged(object? _, FileSystemEventArgs args)
    {
        if (_reading)
            return;
        _reading = true;

        Task.Run(ProcessFileChanges).ContinueWith(_ => _reading = false);
    }

    private async Task ProcessFileChanges()
    {
        try
        {
            await using var fileLock = FileLock.Aquire(filePath);

            var reader = fileLock.Reader;
            if (_lastStreamPosition > reader.BaseStream.Length)
            {
                Log.Verbose("{Path} file was truncated, resetting stream position", filePath);
                await CatchUpAsync(fileLock);
                Log.Debug("The log file has been reset, likely indicates MuMu is starting up");
                Log.Information("MuMu Player is starting up");
                return;
            }

            // We read new things being added from here onwards
            reader.BaseStream.Seek(_lastStreamPosition, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                _lastStreamPosition = reader.BaseStream.Position;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                AppLifetimeParser.MutateLifetime(line, Sessions, SessionGraveyard);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to read the change in file {FilePath}", filePath);
        }
    }

    private uint _initialLinesRead;

    private async Task<string> ReadLineAsync(StreamReader reader, bool increment = true)
    {
        var retVal = await reader.ReadLineAsync();

        if (increment && retVal != null)
            Interlocked.Increment(ref _initialLinesRead);

        return retVal ?? string.Empty;
    }
    /// <summary>
    /// The method ensures that a Rich Presence will be enabled if a game is running before this program started.
    /// </summary>
    internal async Task GetAllSessionInfos(FileLock fileLock, ObservableCollection<MuMuSessionLifetime> activeLifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var reader = fileLock.Reader;
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        _initialLinesRead = 0;

        while (!reader.EndOfStream)
        {
            var line = await ReadLineAsync(reader);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            AppLifetimeParser.MutateLifetime(line, activeLifetimes, graveyard);
        }
    }

    public void Stop()
    {
        _started = false;
        _logWatcher.Dispose();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
    }
}
