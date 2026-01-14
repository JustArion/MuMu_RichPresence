using Polly;
using Polly.Retry;
using FileAccess = System.IO.FileAccess;

namespace Dawn.MuMu.RichPresence.Tools;

internal sealed class FileLock : IAsyncDisposable
{
    private const int MAX_RETRIES = 3;
    private static readonly RetryPolicy<FileStream> _retryPolicy = Policy<FileStream>
        .Handle<IOException>()
        .WaitAndRetry(MAX_RETRIES, _ => TimeSpan.FromMilliseconds(50));

    private readonly FileStream _fileLock;
    public StreamReader Reader { get; }

    public FileInfo LockFile { get; }

    private FileLock(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(nameof(filePath));

        LockFile = new (filePath);

        _fileLock = _retryPolicy.Execute(() => File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        // _fileLock = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Reader = new StreamReader(_fileLock);
    }

    public static FileLock Aquire(string filePath, out StreamReader reader)
    {
        var fileLock = new FileLock(filePath);
        reader = fileLock.Reader;
        return fileLock;
    }
    public static FileLock Aquire(string filePath) => new(filePath);

    public async ValueTask DisposeAsync()
    {
        Reader.Dispose();
        await _fileLock.DisposeAsync();
    }
}
