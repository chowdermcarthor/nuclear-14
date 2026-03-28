using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Gives power armor a separate HP pool (integrity) that absorbs incoming
///     damage before it reaches the wearer.
///
///     While integrity is above zero, <see cref="BleedthroughRatio"/> of each
///     hit (default 1.5%) bleeds through to the player — just enough to feel
///     the impact — and the rest is absorbed by the armor's own HP pool.
///
///     When integrity reaches zero the ArmorComponent is stripped and the
///     wearer takes full unmitigated damage until the suit is repaired with
///     a welder.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerArmorIntegrityComponent : Component
{
    /// <summary>
    ///     Maximum integrity (HP) the armor can have. Tracks how much total
    ///     damage the armor can absorb before breaking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxIntegrity = 200;

    /// <summary>
    ///     Fraction of post-coefficient damage that bleeds through to the
    ///     wearer while integrity is above zero (0.0–1.0).
    ///     Default 0.015 = 1.5% bleedthrough — player feels hits but is
    ///     effectively protected until the armor breaks.
    ///     The remaining (1 − BleedthroughRatio) is absorbed by the armor HP pool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BleedthroughRatio = 0.100f;

    /// <summary>
    ///     When integrity drops to zero the armor is broken and provides no
    ///     absorption. The wearer takes full damage until the suit is repaired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>
    ///     Stores the <see cref="DamageModifierSet"/> from
    ///     <see cref="Content.Shared.Armor.ArmorComponent"/> while the suit is
    ///     broken. The ArmorComponent is removed when integrity hits zero;
    ///     this cache lets us restore it on repair.
    ///     Not networked — only the server needs this.
    /// </summary>
    [DataField]
    public DamageModifierSet? CachedArmorModifiers;

    /// <summary>
    ///     Alert prototype shown on the wearer's HUD while the armor is equipped.
    ///     Severity scales with remaining integrity (higher = healthier).
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> IntegrityAlert = "PowerArmorIntegrity";

    /// <summary>
    ///     Number of severity levels for the HUD alert (matches the icon count
    ///     in the alert prototype).
    /// </summary>
    [DataField]
    public int AlertLevels = 5;
}

/// <summary>
///     Placed on the <b>wearer</b> (not the armor item) while a power armor
///     suit is actively worn. Allows external systems (e.g. a friend with a
///     welder) to forward interactions through the player directly to the
///     armor entity sitting in their inventory.
/// </summary>
[RegisterComponent]
public sealed partial class PowerArmorWornComponent : Component
{
    /// <summary>The armor item currently equipped by this entity.</summary>
    [DataField]
    public EntityUid Armor;
}
