using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence;

/// <summary>
/// Application features differs from LaunchArgs as LaunchArgs is immutable
/// </summary>
internal partial class ApplicationFeatures : ObservableObject
{
    public ApplicationFeatures() =>
        this.WhenPropertyChanged(x => x.RichPresenceEnabled)
            .Subscribe(value =>
                Log.Verbose($"ApplicationFeature changed {nameof(RichPresenceEnabled)} ({{Value}})",  value.Value));

    [ObservableProperty]
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
    public partial bool RichPresenceEnabled { get; set; }
}
