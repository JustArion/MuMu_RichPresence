using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dawn.MuMu.RichPresence.Models;

public sealed record MuMuSessionLifetime : INotifyPropertyChanged
{
    public required string PackageName { get; init => SetField(ref field, value); }

    public required HistoricalEntry<AppState> AppState { get; init => SetField(ref field, value); }

    public required string Title { get; set => SetField(ref field, value); }

    public CancellationTokenSource? SessionSubscriptions { get; set => SetField(ref field, value); }

    /// <summary>
    /// An <b>unordered</b> collection of log entries regarding this package name
    /// </summary>
    [SuppressMessage("ReSharper", "CollectionNeverQueried.Global")]
    public ObservableCollection<string> PackageLifetimeEntries { get; } = [];

    public DateTimeOffset StartTime { get; set => SetField(ref field, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

}
