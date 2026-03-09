// #Misfits Change - Ported from Delta-V chronic pain system
using Content.Shared.Dataset;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Misfits.ChronicPain.Components;

/// <summary>
///     Marks an entity as suffering from chronic pain — a persistent condition that causes periodic
///     pain popups and a visual effect, suppressed only by pain medication.
///     Thematically appropriate for Fallout's wasteland setting where old injuries never fully heal.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChronicPainComponent : Component
{
    /// <summary>
    ///     Default suppression duration when no duration is specified.
    /// </summary>
    [DataField]
    public TimeSpan DefaultSuppressionTime = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     How long pain is suppressed on initialization so players don't need to take a pill immediately.
    /// </summary>
    [DataField]
    public TimeSpan DefaultSuppressionTimeOnInit = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     When to next tick and check for popup/suppression state.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan NextUpdateTime = TimeSpan.Zero;

    /// <summary>
    ///     When to show the next pain effect popup.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan NextPopupTime = TimeSpan.Zero;

    /// <summary>
    ///     Pain is suppressed while CurTime is before this timestamp.
    ///     Set by medication or medical treatment.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan SuppressionEndTime = TimeSpan.Zero;

    /// <summary>
    ///     The dataset of pain messages to display.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> DatasetPrototype = "PainEffects";

    /// <summary>
    ///     Minimum time between pain popups.
    /// </summary>
    [DataField]
    public TimeSpan MinimumPopupDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum time between pain popups.
    /// </summary>
    [DataField]
    public TimeSpan MaximumPopupDelay = TimeSpan.FromSeconds(40);
}
