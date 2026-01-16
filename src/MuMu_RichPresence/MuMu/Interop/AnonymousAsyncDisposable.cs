namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public class AnonymousAsyncDisposable(Func<ValueTask>? dispose = null) : IAsyncDisposable
{
    public static readonly IAsyncDisposable None = new AnonymousAsyncDisposable();
    public async ValueTask DisposeAsync()
    {
        if (dispose == null)
            return;

        GC.SuppressFinalize(this);
        await dispose();
    }
}
