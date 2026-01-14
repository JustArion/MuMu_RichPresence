namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public class AnonymousAsyncDisposable(Func<ValueTask> dispose) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await dispose();
    }
}
