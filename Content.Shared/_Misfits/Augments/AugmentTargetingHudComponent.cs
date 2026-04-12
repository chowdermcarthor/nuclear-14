// #Misfits Add - Vault-Tec Targeting HUD augment component.
// Passive subdermal implant that reduces weapon spread while installed.
// Inspired by Goob-Station SmartLink; clean-room reimplementation.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Passive subdermal implant that reduces gun spread.
/// Effect is always active while implanted — no toggle action needed.
/// </summary>
[RegisterComponent]
public sealed partial class AugmentTargetingHudComponent : Component
{
    /// <summary>Fractional reduction to gun spread (0.3 = 30% tighter).</summary>
    [DataField]
    public float SpreadReduction = 0.3f;
}

/// <summary>
/// Marker placed on the body entity while the targeting HUD is installed.
/// Enables gun spread reduction via the GunRefreshModifiers event.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentTargetingHudActiveComponent : Component
{
    /// <summary>Fractional spread reduction to apply.</summary>
    [DataField, AutoNetworkedField]
    public float SpreadReduction = 0.3f;
}
