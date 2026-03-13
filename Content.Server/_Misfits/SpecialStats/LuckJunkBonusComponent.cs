// #Misfits Add - Marks a storage entity as eligible for the Luck SPECIAL bonus.
// When a player with a high Luck stat opens this storage, SpecialLuckSystem may
// spawn an additional item from LuckyItems at the pile's location.
// Added to N14JunkPileBase so all junk pile variants inherit it automatically.

using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.SpecialStats;

/// <summary>
/// Attached to any storage entity that should participate in the Luck S.P.E.C.I.A.L.
/// bonus. When opened by a player, <see cref="SpecialLuckSystem"/> rolls against
/// their Luck stat and may spawn a bonus item from <see cref="LuckyItems"/>.
/// </summary>
[RegisterComponent]
public sealed partial class LuckJunkBonusComponent : Component
{
    /// <summary>
    /// Pool of prototype IDs to randomly draw a bonus item from.
    /// Override in YAML per-entity for custom loot tables.
    /// </summary>
    [DataField]
    public List<EntProtoId> LuckyItems = new()
    {
        "N14Stimpak",
        "N14CurrencyCap",
        "N14MagazinePistol10mm",
        "N14MagazinePistol9mm",
    };

    /// <summary>
    /// Additional roll-success probability per Luck point above 1.
    /// LCK 1 = 0%, LCK 10 = 9 × ChancePerLuckPoint.
    /// At the default 0.06: LCK 10 = 54% chance per opening.
    /// </summary>
    [DataField]
    public float ChancePerLuckPoint = 0.06f;
}
