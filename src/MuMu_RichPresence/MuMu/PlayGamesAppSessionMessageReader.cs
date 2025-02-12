// #define LOG_APP_SESSION_MESSAGES

using Dawn.MuMu.RichPresence.Domain;
using Dawn.MuMu.RichPresence.PlayGames.FileOperations;
using Dawn.Serilog.CustomEnrichers;

namespace Dawn.MuMu.RichPresence.PlayGames;

using System.Text;
using global::Serilog;
using FileAccess = System.IO.FileAccess;

public class PlayGamesAppSessionMessageReader(string filePath) : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<PlayGamesAppSessionMessageReader>();

    private bool _started;
    private long _lastStreamPosition;
    private readonly PlayGamesLogWatcher _logWatcher = new(filePath);
    public event EventHandler<PlayGamesSessionInfo>? OnSessionInfoReceived;

    public void StartAsync()
    {
        if (_started)
            return;
        _started = true;

        Task.Factory.StartNew(InitiateWatchOperation, TaskCreationOptions.LongRunning);
    }
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
        var reader = fileLock.Reader;
        IReadOnlyList<PlayGamesSessionInfo> sessions;
        // We read the old entries (To check if there's a game currently running)
        using (Warn.OnLongerThan(TimeSpan.FromSeconds(2), "Catch-Up took unusually long"))
            sessions = await GetAllSessionInfos(fileLock);
        _lastStreamPosition = reader.BaseStream.Position;

        var last = sessions.Count > 0
            ? sessions[^1]
            : null;

        if (last is { AppState: AppSessionState.Running })
        {
            Log.Verbose("Caught up (Processed {EventsProcessed} events), emitting {SessionInfo}", sessions.Count, last);
            OnSessionInfoReceived?.Invoke(this, last);
        }
        else
            Log.Verbose("Caught up, no games are currently running (Processed {EventsProcessed} events)", sessions.Count);

        Log.Debug("CatchUp: Stream position is currently at {Position}", reader.BaseStream.Position);
        Log.Debug("CatchUp: Read {Lines} lines", _initialLinesRead);
        Log.Debug("CatchUp: File Size is currently {FileSizeMb} MB", Math.Round(reader.BaseStream.Length / Math.Pow(1024, 2), 0));
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
                return;
            }

            // We read new things being added from here onwards
            reader.BaseStream.Seek(_lastStreamPosition, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                _lastStreamPosition = reader.BaseStream.Position;
                await ProcessLogChunkAsync(line, reader);
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
    internal async Task<IReadOnlyList<PlayGamesSessionInfo>> GetAllSessionInfos(FileLock fileLock)
    {
        var reader = fileLock.Reader;
        Log.Verbose("Catching up...");
        var sessions = new List<PlayGamesSessionInfo>(20);
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        _initialLinesRead = 0;

        while (!reader.EndOfStream)
        {
            var line = await ReadLineAsync(reader);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            PlayGamesSessionInfo? sessionInfo = null;
            if (line.Contains("AppSessionModule: sessions updated:"))
            {
                var appSessionMessage = await ExtractLogEntry(reader, line);
                sessionInfo = AppSessionInfoBuilder.BuildFromAppSession(appSessionMessage);

            }
            else if (line.Contains("Emulator state updated:"))
            {
                var appSessionMessage = await ExtractLogEntry(reader, line);
                sessionInfo = AppSessionInfoBuilder.Build(appSessionMessage);
            }

            if (sessionInfo == null)
                continue;

            sessions.Add(sessionInfo);
        }

        return sessions;
    }

    private async Task ProcessLogChunkAsync(string? line, StreamReader reader)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        PlayGamesSessionInfo? sessionInfo = null;
        if (line.Contains("AppSessionModule: sessions updated:"))
        {
            // This delay is needed to let the GPG finish writing the entry to the file as its a multi-line log
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var appSessionMessage = await ExtractLogEntry(reader, line, false);
            sessionInfo = AppSessionInfoBuilder.BuildFromAppSession(appSessionMessage);

        }
        else if (line.Contains("Emulator state updated:"))
        {
            // This delay is needed to let the GPG finish writing the entry to the file as its a multi-line log
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var appSessionMessage = await ExtractLogEntry(reader, line, false);
            sessionInfo = AppSessionInfoBuilder.Build(appSessionMessage);
        }

        if (sessionInfo == null)
            return;

        OnSessionInfoReceived?.Invoke(this, sessionInfo);
    }

    private async Task<string> ExtractLogEntry(StreamReader reader, string line, bool increment = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine(line);
        line = await ReadLineAsync(reader, false);

        while (!string.IsNullOrWhiteSpace(line) && line != "}")
        {
            sb.AppendLine(line);
            line = await ReadLineAsync(reader, increment);
        }

        sb.AppendLine("}");
        var appSessionMessage = sb.ToString();
        #if LOG_APP_SESSION_MESSAGES
        _logger.Debug("Received AppSession Message: \n{Line}", appSessionMessage);
        #endif
        return appSessionMessage;
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
