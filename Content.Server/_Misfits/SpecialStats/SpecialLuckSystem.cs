// #Misfits Add - Server system that grants a bonus item when a lucky player opens a junk pile.
// Roll chance scales with Luck SPECIAL stat. One roll per player per pile per round
// (tracked in _alreadyRolled to prevent farming from re-opening the same pile).

using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Server._Misfits.SpecialStats;

/// <summary>
/// Hooks into <see cref="StorageComponent"/> BUI open events.
/// When the opener has a <see cref="PersistentPlayerDataComponent"/> and the
/// storage has a <see cref="LuckJunkBonusComponent"/>, rolls for a bonus item
/// based on the player's Luck stat.
/// </summary>
public sealed class SpecialLuckSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    // Per-pile tracking: [pile entity → set of player entities that have already rolled this round].
    // Prevents farming the same pile by re-opening it repeatedly.
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _alreadyRolled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LuckJunkBonusComponent, ComponentShutdown>(OnLuckCompShutdown);
        // #Misfits Fix: subscribe on LuckJunkBonusComponent (not StorageComponent) to avoid
        // duplicate subscription conflict with SharedStorageSystem.Initialize().
        SubscribeLocalEvent<LuckJunkBonusComponent, BoundUIOpenedEvent>(OnStorageOpened);

        // Clear tracking between rounds so next-round respawned piles start fresh.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _alreadyRolled.Clear();
    }

    private void OnLuckCompShutdown(Entity<LuckJunkBonusComponent> ent, ref ComponentShutdown args)
    {
        // Clean up tracking when the pile is destroyed.
        _alreadyRolled.Remove(ent.Owner);
    }

    private void OnStorageOpened(Entity<LuckJunkBonusComponent> ent, ref BoundUIOpenedEvent args)
    {
        // ent.Comp is LuckJunkBonusComponent — entity is guaranteed to have it.
        var luck = ent.Comp;

        var actor = args.Actor;
        if (!TryComp<PersistentPlayerDataComponent>(actor, out var playerData))
            return;

        // One roll per player per pile per round.
        if (!_alreadyRolled.TryGetValue(ent.Owner, out var rolledSet))
        {
            rolledSet = new HashSet<EntityUid>();
            _alreadyRolled[ent.Owner] = rolledSet;
        }

        if (!rolledSet.Add(actor))
            return; // this player already rolled on this pile

        // Roll: each Luck point above 1 adds ChancePerLuckPoint success probability.
        // LCK 1 = 0%, LCK 10 = 54% (with default 0.06 per point).
        var luckStat = playerData.Luck;
        if (luckStat <= 1)
            return;

        var rollChance = (luckStat - 1) * luck.ChancePerLuckPoint;
        if (!_random.Prob(rollChance))
            return;

        // Choose a random item from the lucky loot pool and spawn it at the pile.
        if (luck.LuckyItems.Count == 0)
            return;

        var chosenProto = _random.Pick(luck.LuckyItems);
        var coords = Transform(ent.Owner).Coordinates;
        Spawn(chosenProto, coords);
    }
}
