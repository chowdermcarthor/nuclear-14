using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Gives power armor a separate HP pool (integrity) that absorbs incoming
///     damage before it reaches the wearer. The armor item must also have a
///     <see cref="Content.Shared.Damage.Components.DamageableComponent"/> to
///     track its own damage state.
///
///     When integrity is depleted the armor stops absorbing damage entirely
///     and all hits pass through to the player.
///
///     Tiered degradation: as the armor takes damage its absorption ratio
///     decreases through configurable thresholds, so a heavily-damaged suit
///     provides worse protection.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerArmorIntegrityComponent : Component
{
    /// <summary>
    ///     Maximum integrity (HP) the armor can have.
    ///     This should match or relate to the Damageable component's damage cap.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxIntegrity = 200;

    /// <summary>
    ///     Fraction of incoming damage the armor absorbs at full integrity
    ///     (0.0 – 1.0). The remainder reaches the wearer immediately.
    ///     Example: 0.80 = armor eats 80 %, player takes 20 %.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AbsorptionRatio = 0.50f;

    /// <summary>
    ///     Ordered list of degradation tiers. Each entry defines a threshold
    ///     (fraction of max integrity remaining) and the absorption ratio that
    ///     applies once integrity drops below that fraction.
    ///     Entries MUST be ordered from highest threshold to lowest.
    ///     Example: { 0.66, 0.35 } means "below 66 % integrity → absorb 35 %".
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<DegradationTier> DegradationTiers = new()
    {
        new DegradationTier { Threshold = 0.66f, Absorption = 0.35f },
        new DegradationTier { Threshold = 0.33f, Absorption = 0.20f },
    };

    /// <summary>
    ///     Per-hit damage threshold below which the armor absorbs normally.
    ///     Hits above this overwhelm the plating and absorption scales down.
    ///     At <see cref="PenetrationCap"/> damage the armor absorbs nothing.
    ///     Models how heavy-calibre rounds (.308+) and apex predators punch
    ///     through while lighter rounds (9 mm, 5.56) are well-contained.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PenetrationThreshold = 20f;

    /// <summary>
    ///     Damage value at or above which the hit fully overwhelms the armor
    ///     (effective absorption → 0). Must be greater than
    ///     <see cref="PenetrationThreshold"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PenetrationCap = 50f;

    /// <summary>
    ///     When integrity drops to zero the armor is considered broken and
    ///     provides no absorption at all. This flag tracks that state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>
    ///     Alert prototype shown on the wearer's HUD while the armor is equipped.
    ///     Severity scales with remaining integrity (higher = healthier).
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> IntegrityAlert = "PowerArmorIntegrity";

    /// <summary>
    ///     Number of severity levels for the HUD alert (matches the icon count
    ///     in the alert prototype). Used with ContentHelpers.RoundToLevels.
    /// </summary>
    [DataField]
    public int AlertLevels = 5;
}

/// <summary>
///     A single degradation tier: when remaining integrity is below
///     <see cref="Threshold"/> (as a fraction of max), the armor's
///     effective absorption ratio drops to <see cref="Absorption"/>.
/// </summary>
[DataDefinition]
public sealed partial class DegradationTier
{
    [DataField]
    public float Threshold;

    [DataField]
    public float Absorption;
}
