// #define LOG_APP_SESSION_MESSAGES

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dawn.MuMu.RichPresence.Logging.Serilog;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.MuMu.Interop;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu;

public class MuMuPlayerLogReader(string filePath, MuMuProcessState currentProcessState) : IDisposable
{
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
        Log.Verbose("Doing fresh read-operation pass on file {Path}", Path.GetFileName(filePath));

        // Wait till the file exists
        if (!File.Exists(filePath))
        {
            Log.Debug("File not found: Shell.log");
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
            var fileSizeReadMB = 0D;


            // We read the old entries (To check if there's a game currently running)
            using (Warn.OnLongerThan(TimeSpan.FromSeconds(2), "Catch-Up took unusually long"))
            {
                var file = fileLock.LockFile;
                var fileDirectory = file.Directory!;

                var oldLogFiles = fileDirectory.GetFiles("shell*")
                    .Where(x => x.FullName !=
                                file.FullName) // We already have a file lock on this, we need to process it last
                    .OrderBy(x => x.LastWriteTimeUtc)
                    .ToArray();

                Log.Verbose("Checking through {LogFileCount} shell.log files", oldLogFiles.Length);

                foreach (var oldLogFile in oldLogFiles)
                {
                    if (oldLogFile.FullName == file.FullName)
                        continue;

                    await using var oldFileLock = FileLock.Aquire(oldLogFile.FullName);
                    await GetAllSessionInfos(oldFileLock, sessions, SessionGraveyard);
                    fileSizeReadMB += oldFileLock.Reader.BaseStream.Length / Math.Pow(1024, 2);
                }

                await GetAllSessionInfos(fileLock, sessions, SessionGraveyard);
                fileSizeReadMB += reader.BaseStream.Length / Math.Pow(1024, 2);
            }
            _lastStreamPosition = reader.BaseStream.Position;

            // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            // I have noticed that very rarely the session lifetime (x) can be null
            // TODO: we should investigate the root cause later, for now we treat it as nullable
            var processedEvents = sessions.Select(x => x?.PackageLifetimeEntries?.Count ?? 0).Sum() +
                                  SessionGraveyard.Select(x => x?.PackageLifetimeEntries?.Count ?? 0).Sum();
            // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

            Sessions.Clear();
            foreach (var session in sessions)
                Sessions.Add(session);

            // Emitting means that there's a game currently running AFTER we started, meaning we flag it for the Rich Presence to trigger
            if (sessions.FirstOrDefault(x => x.AppState.Value is AppState.Focused or AppState.Started) is { } lifetime)
                Log.Verbose(
                    "Caught up in {ExecutionDuration:F}ms (Processed {EventsProcessed} events), emitting {SessionInfo}",
                    Stopwatch.GetElapsedTime(ts).TotalMilliseconds, processedEvents, lifetime);
            else
            {
                var game = await MuMuADB.TryFindGame();
                if (!game.HasValue)
                    Log.Verbose("Caught up in {ExecutionDuration:F}ms, no games are currently running (Processed {EventsProcessed} events)", Stopwatch.GetElapsedTime(ts).TotalMilliseconds, sessions.Count);
                else
                {
                    var info = game.Value;
                    Sessions.Add(new MuMuSessionLifetime {
                        AppState = new() { Value = AppState.Focused },
                        Title = info.Title,
                        PackageName = info.AppInfo.PackageName,
                        StartTime = info.AppInfo.StartTime
                    });

                    Log.Debug("Set app session via ADB");
                }
            }


            Log.Debug("CatchUp: Read {FileSize:F2}mb", Math.Round(fileSizeReadMB, 2));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read the file {FilePath}", filePath);
        }
    }

    private void LogFileWatcherOnError(object? _, ErrorEventArgs e) => Log.Error(e.GetException(), "File watcher error");

    private bool _reading;
    private void LogFileWatcherOnFileChanged(object? _, FileSystemEventArgs args)
    {
        //              The file might not exist yet, this happens when MuMu clears the file and makes a new one
        if (_reading || !File.Exists(filePath))
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
                var prevSize = Math.Round(_lastStreamPosition / Math.Pow(1024, 1), 2);

                if (currentProcessState.CurrentEmulatorProcess == null)
                {
                    Log.Verbose("{Path} file was truncated, resetting stream position", filePath);
                    await CatchUpAsync(fileLock);
                    Log.Debug("The log file has been reset, likely indicates MuMu is starting up, previous size: {PreviousSize:F2} kb", prevSize);
                    Log.Information("MuMu Player is starting up");
                    return;
                }

                var newPosition = reader.BaseStream.Position;
                // What is a running truncation event?
                // It's when the file gets cleaned out (there's nothing in it anymore) while the emulator is running (opposed to starting up)
                // - We read from the new position of the stream (probably 0)
                Log.Debug("Observed a running truncation event. (MuMu wipes the log file while the app is currently running), previous size: {PreviousSize:F2} kb, new position: {Position}", prevSize, newPosition);
                _lastStreamPosition = newPosition;
            }

            // We read new things being added from here onwards
            reader.BaseStream.Seek(_lastStreamPosition, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            while (await reader.ReadLineAsync() is { } line)
            {
                _lastStreamPosition = reader.BaseStream.Position;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                AppLifetimeParser.MutateLifetime(line, Sessions, SessionGraveyard);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read the change in file {FilePath}", filePath);
        }
    }

    private uint _initialLinesRead;

    private async Task<string?> ReadLineAsync(StreamReader reader, bool increment = true)
    {
        var retVal = await reader.ReadLineAsync();

        if (increment && retVal != null)
            Interlocked.Increment(ref _initialLinesRead);

        return retVal;
    }
    /// <summary>
    /// The method ensures that a Rich Presence will be enabled if a game is running before this program started.
    /// </summary>
    internal async Task GetAllSessionInfos(FileLock fileLock, ObservableCollection<MuMuSessionLifetime> activeLifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var reader = fileLock.Reader;
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        _initialLinesRead = 0;

        while (await ReadLineAsync(reader) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            AppLifetimeParser.MutateLifetime(line, activeLifetimes, graveyard);
        }
    }

    public MuMuSessionLifetime? GetFocusedApp()
    {
        foreach (var session in Sessions)
        {
            if (AppLifetimeParser.IsSystemLevelPackage(session.PackageName))
                continue;

            if (session.AppState.Value is AppState.Focused or AppState.Started)
                return session;
        }

        return null;
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
