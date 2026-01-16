namespace Dawn.MuMu.RichPresence.Extensions;

public static class TaskEx
{
    extension(Task task)
    {
        public void Catch(Action<Exception> action)
        {
            task.ContinueWith(t =>
            {
                if (!t.IsFaulted)
                    return t;

                action(t.Exception);
                return Task.CompletedTask;
            });
        }
    }

    extension<T>(Task<T?> task) where T : class
    {
        public Task<T?> Catch(Action<Exception> action)
        {
            return task.ContinueWith<T?>(t =>
            {
                if (!t.IsFaulted)
                    return t.Result;

                action(t.Exception);
                return null;
            });
        }
    }
}
