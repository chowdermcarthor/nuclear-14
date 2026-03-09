// #Misfits Change - Ported from Delta-V surgery contamination system
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Component that allows an entity to be cross contaminated from being used in surgery.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SurgeryCrossContaminationComponent : Component
{
    /// <summary>
    ///     Patient DNAs that are present on this dirtied tool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> DNAs = new();
}
