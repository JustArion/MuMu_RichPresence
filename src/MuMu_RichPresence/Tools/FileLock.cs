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

    private FileLock(FileInfo filePath)
    {
        if (!filePath.Exists)
            throw new FileNotFoundException(nameof(filePath));

        LockFile = filePath;

        _fileLock = _retryPolicy.Execute(() => File.Open(filePath.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        // _fileLock = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Reader = new StreamReader(_fileLock);
    }

    public static FileLock Aquire(FileInfo fileInfo) => new(fileInfo);

    public async ValueTask DisposeAsync()
    {
        Reader.Dispose();
        await _fileLock.DisposeAsync();
    }
}
