using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Dawn.MuMu.RichPresence.Extensions;

public static class IObservableEx
{
    extension<T>(IObservable<T> observable)
    {
        public IDisposable SubscribeGuarded(Action<T> onNext, Action<Exception>? onError = null)
        {
            return Observable.Defer(() => observable)
                .SelectMany(item => Observable.Defer(() =>
                    {
                        onNext(item);
                        return Observable.Empty<Unit>();
                    })
                    .Catch<Unit, Exception>(ex =>
                    {
                        onError?.Invoke(ex);
                        return Observable.Empty<Unit>();
                    }))
                .Catch<Unit, Exception>(ex =>
                {
                    onError?.Invoke(ex);
                    return Observable.Empty<Unit>();
                })
                .Subscribe();
        }

        public IDisposable SubscribeAsync(IScheduler scheduler, Func<T, Task> action, Action<Exception>? onError = null) =>
            observable
                .SelectMany(item => Observable.FromAsync(() => action(item))
                    .SubscribeOn(scheduler)
                    .Catch<Unit, Exception>(ex =>
                    {
                        onError?.Invoke(ex);
                        return Observable.Empty<Unit>();
                    }))
                .Subscribe();

        public IDisposable SubscribeAsync(Func<T, Task> action, Action<Exception>? onError = null) =>
            observable
                .SelectMany(item => Observable.FromAsync(() => action(item))
                    .SubscribeOn(TaskPoolScheduler.Default)
                    .Catch<Unit, Exception>(ex =>
                    {
                        onError?.Invoke(ex);
                        return Observable.Empty<Unit>();
                    }))
                .Subscribe();

        public IObservable<T> WhereSafe(Predicate<T> predicate, Action<Exception>? onError = null) =>
            observable.SelectMany(item =>
                Observable.Defer(() => predicate(item)
                        ? Observable.Return(item)
                        : Observable.Empty<T>())
                    .Catch<T, Exception>(ex =>
                    {
                        onError?.Invoke(ex);
                        return Observable.Empty<T>();
                    }));

        public IObservable<TResult> SelectManySafe<TResult>(Func<T, IObservable<TResult>> selector, Action<Exception>? onError = null) =>
            observable.SelectMany(item =>
                Observable.Defer(() => selector(item))
                    .Catch<TResult, Exception>(ex =>
                    {
                        onError?.Invoke(ex);
                        return Observable.Empty<TResult>();
                    }));
    }
}
