using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence;

/// <summary>
/// Application features differs from LaunchArgs as LaunchArgs is immutable
/// </summary>
internal partial class ApplicationFeatures : ObservableObject
{
    public ApplicationFeatures()
    {
        // We should prob use some sort of reflection or smthn to show which property changed, and it's value, but I can't find how to rn, so we use duplication :)
        this.WhenPropertyChanged(x => RichPresenceEnabled)
            .Subscribe(value =>
                Log.Verbose($"ApplicationFeature changed {nameof(RichPresenceEnabled)} ({{Value}})", value.Value));

        this.WhenPropertyChanged(x => CheckPreReleases)
            .Subscribe(value =>
                Log.Verbose($"ApplicationFeature changed {nameof(CheckPreReleases)} ({{Value}})", value.Value));
    }

    public void Sync()
    {
        CheckPreReleases = Arguments.CheckPreReleases;
        RichPresenceEnabled = Arguments.RichPresenceEnabledOnStart;
    }

    [ObservableProperty]
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
    public partial bool RichPresenceEnabled { get; set; }


    [ObservableProperty]
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
    public partial bool CheckPreReleases { get; set; }
}
