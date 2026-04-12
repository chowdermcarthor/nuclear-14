// #Misfits Add - Vault-Tec Retractable Blades augment component.
// Subdermal implant that grants a toggle action to deploy/retract hidden arm blades.
// Inspired by Goob-Station MantisBlades; clean-room reimplementation.

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Subdermal implant component that enables retractable arm blades.
/// When toggled, spawns a melee weapon entity in the user's hand.
/// </summary>
[RegisterComponent]
public sealed partial class AugmentRetractableBladesComponent : Component
{
    /// <summary>Prototype ID of the blade weapon entity to spawn on deploy.</summary>
    [DataField]
    public EntProtoId BladePrototype = "MisfitsAugmentBlade";

    /// <summary>Sound played when blades extend.</summary>
    [DataField]
    public SoundSpecifier? DeploySound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    /// <summary>Sound played when blades retract.</summary>
    [DataField]
    public SoundSpecifier? RetractSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    /// <summary>Whether the blades are currently deployed.</summary>
    [ViewVariables]
    public bool Deployed;

    /// <summary>Reference to the spawned blade weapon entity.</summary>
    [ViewVariables]
    public EntityUid? BladeEntity;
}

/// <summary>Action event raised when the user toggles retractable blades.</summary>
public sealed partial class ToggleRetractableBladesEvent : InstantActionEvent;
