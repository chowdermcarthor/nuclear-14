// #Misfits Add - Vault-Tec Combat Stim Injector augment component.
// Subdermal implant that grants temporary melee damage boost + damage resistance,
// then inflicts Poison damage when the effect expires.
// Inspired by Goob-Station BerserkerImplant; clean-room reimplementation.

using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Subdermal implant that activates a temporary combat buff:
/// increased melee damage and damage resistance, followed by toxin poisoning on expiry.
/// </summary>
[RegisterComponent]
public sealed partial class AugmentCombatStimComponent : Component
{
    /// <summary>Melee damage multiplier during buff.</summary>
    [DataField]
    public float MeleeDamageMultiplier = 1.35f;

    /// <summary>DamageModifierSet prototype applied to incoming damage during buff.</summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype> DamageResistanceSet = "MisfitsCombatStimResistance";

    /// <summary>How long the combat buff lasts.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);

    /// <summary>Poison damage dealt when the buff expires.</summary>
    [DataField]
    public float ToxinDamageOnExpiry = 25f;

    /// <summary>Sound played on activation.</summary>
    [DataField]
    public SoundSpecifier? ActivateSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

/// <summary>
/// Marker placed on the body entity while combat stim is active.
/// Enables melee boost and damage resistance via modifier events.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentCombatStimActiveComponent : Component
{
    /// <summary>Melee damage multiplier.</summary>
    [DataField, AutoNetworkedField]
    public float MeleeDamageMultiplier = 1.35f;

    /// <summary>DamageModifierSet prototype for incoming damage reduction.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<DamageModifierSetPrototype> DamageResistanceSet = "MisfitsCombatStimResistance";

    /// <summary>When the buff expires (server time).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}

/// <summary>Action event raised when the user activates the combat stim injector.</summary>
public sealed partial class ActivateCombatStimEvent : InstantActionEvent;
