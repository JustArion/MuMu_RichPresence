using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence.Models;

public sealed class HistoricalEntry<TEntry> : INotifyPropertyChanged where TEntry : notnull
{
    public HistoricalEntry() => this.WhenPropertyChanged(x => x.Value).Subscribe(x => History.Add(x.Value!));
    public required TEntry Value { get; set => SetField(ref field, value); }
    public ObservableCollection<TEntry> History { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public static implicit operator TEntry(HistoricalEntry<TEntry> entry) => entry.Value;
    public static implicit operator HistoricalEntry<TEntry>(TEntry entry) => new() { Value = entry };

    public override string ToString() => Value.ToString() ?? string.Empty;
}
