using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Linq;

namespace Dawn.MuMu.RichPresence.Extensions;

public static class ObservableCollectionEx
{
    extension<T>(ObservableCollection<T> collection)
    {
        public IObservable<T> ItemsAdded
        {
            get => Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                        h => collection.CollectionChanged += h,
                        h => collection.CollectionChanged -= h)
                    .Where(e => e.EventArgs is { Action: NotifyCollectionChangedAction.Add, NewItems: not null })
                    .SelectMany(e => e.EventArgs.NewItems!.Cast<T>());
        }

        public IObservable<T> ItemsRemoved
        {
            get => Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => collection.CollectionChanged += h,
                    h => collection.CollectionChanged -= h)
                .Where(e => e.EventArgs is { Action: NotifyCollectionChangedAction.Remove, OldItems: not null })
                .SelectMany(e => e.EventArgs.OldItems!.Cast<T>());
        }
    }
}
