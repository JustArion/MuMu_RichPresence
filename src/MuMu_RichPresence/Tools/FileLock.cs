using FileAccess = System.IO.FileAccess;

namespace Dawn.MuMu.RichPresence.Tools;

internal sealed class FileLock : IAsyncDisposable
{
    private readonly FileStream _fileLock;
    public StreamReader Reader { get; }

    private FileLock(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(nameof(filePath));

        _fileLock = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
