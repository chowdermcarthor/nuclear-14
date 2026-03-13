// #Misfits Add - Temporary SPECIAL stat boosts applied by drug reagents during metabolism.
// Set and expired by DrugSpecialBoostSystem; written each tick by SpecialStatBoostEffect.
// NetworkedComponent so client-side prediction can apply the in-progress effects locally.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Added to a player entity while a SPECIAL-boosting drug is being metabolised.
/// Each field stores the INTEGER delta for that stat while the drug is active.
/// Only deltas for stats that are actually boosted will be non-zero.
/// Managed (refreshed, expired) by <see cref="DrugSpecialBoostSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrugSpecialBoostComponent : Component
{
    /// <summary>Temporary Strength delta — scales melee damage bonus via DrugSpecialBoostSystem.</summary>
    [DataField, AutoNetworkedField]
    public int StrengthBoost;

    /// <summary>Temporary Perception delta — reduces gun spread/recoil (stacks on SpecialPerceptionSystem).</summary>
    [DataField, AutoNetworkedField]
    public int PerceptionBoost;

    /// <summary>Temporary Endurance delta — stored for future dynamic stamina effects.</summary>
    [DataField, AutoNetworkedField]
    public int EnduranceBoost;

    /// <summary>Temporary Charisma delta — stored for future dialogue/barter effects.</summary>
    [DataField, AutoNetworkedField]
    public int CharismaBoost;

    /// <summary>Temporary Agility delta — adds movement speed bonus via DrugSpecialBoostSystem.</summary>
    [DataField, AutoNetworkedField]
    public int AgilityBoost;

    /// <summary>Temporary Intelligence delta — stored for future crafting/XP effects.</summary>
    [DataField, AutoNetworkedField]
    public int IntelligenceBoost;

    /// <summary>Temporary Luck delta — stored for future loot/crit effects.</summary>
    [DataField, AutoNetworkedField]
    public int LuckBoost;

    /// <summary>Game-time at which all boost effects expire.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpireTime = TimeSpan.Zero;
}
