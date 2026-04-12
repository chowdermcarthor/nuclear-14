// #Misfits Add - Vault-Tec Reflex Booster augment component.
// Subdermal implant that grants a temporary speed boost with a knockdown penalty on expiry.
// Inspired by Goob-Station Sandevistan; clean-room reimplementation.

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Subdermal implant that grants a temporary movement speed boost on activation.
/// Penalizes with brief knockdown when the effect expires.
/// </summary>
[RegisterComponent]
public sealed partial class AugmentReflexBoosterComponent : Component
{
    /// <summary>Movement speed multiplier during boost (1.0 = normal).</summary>
    [DataField]
    public float SpeedMultiplier = 1.5f;

    /// <summary>How long the speed boost lasts.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    /// <summary>Knockdown duration when boost expires (adrenaline crash).</summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);

    /// <summary>Sound played on activation.</summary>
    [DataField]
    public SoundSpecifier? ActivateSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

/// <summary>
/// Marker placed on the body entity while the reflex booster is active.
/// Used by the shared speed modifier handler for client prediction.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentReflexBoosterActiveComponent : Component
{
    /// <summary>Speed multiplier to apply to movement.</summary>
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.5f;

    /// <summary>When the buff expires (server time).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}

/// <summary>Action event raised when the user activates the reflex booster.</summary>
public sealed partial class ActivateReflexBoosterEvent : InstantActionEvent;
