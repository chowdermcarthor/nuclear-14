// #Misfits Change: Keep hostile robot NPC mobs neutral to player robot species until a player robot attacks.
// Mirrors the MobGhoulAggroSystem pattern used for feral ghouls.
using Content.Server.GameTicking;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Robot;

public sealed class MobRobotAggroSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan NeutralSyncInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextNeutralSync;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobRobotAggroComponent, ComponentStartup>(OnMobRobotStartup);
        SubscribeLocalEvent<MobRobotAggroComponent, DamageChangedEvent>(OnMobRobotDamaged);
        SubscribeLocalEvent<MobRobotAggroComponent, DisarmedEvent>(OnMobRobotDisarmed);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextNeutralSync)
            return;

        _nextNeutralSync = _timing.CurTime + NeutralSyncInterval;
        SyncNeutralPlayerRobots();
    }

    private void OnMobRobotStartup(Entity<MobRobotAggroComponent> ent, ref ComponentStartup args)
    {
        SyncNeutralPlayerRobots(ent);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!IsPlayerRobot(args.Mob))
            return;

        SyncNeutralPlayerRobot(args.Mob);
    }

    private void OnMobRobotDamaged(Entity<MobRobotAggroComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } attacker)
            return;

        if (!IsPlayerRobot(attacker))
            return;

        ProvokeAllMobRobots(attacker);
    }

    private void OnMobRobotDisarmed(Entity<MobRobotAggroComponent> ent, ref DisarmedEvent args)
    {
        if (!IsPlayerRobot(args.Source))
            return;

        ProvokeAllMobRobots(args.Source);
    }

    private void SyncNeutralPlayerRobots(Entity<MobRobotAggroComponent> ent)
    {
        EnsureComp<FactionExceptionComponent>(ent);
        SyncNeutralPlayerRobots();
    }

    private void SyncNeutralPlayerRobots()
    {
        var playerRobots = new ValueList<EntityUid>();
        var playerQuery = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent>();
        while (playerQuery.MoveNext(out var playerUid, out _, out var humanoid))
        {
            if (IsRobotSpecies(humanoid))
                playerRobots.Add(playerUid);
        }

        var robotQuery = EntityQueryEnumerator<MobRobotAggroComponent, FactionExceptionComponent>();
        while (robotQuery.MoveNext(out var robotUid, out var aggro, out var exception))
        {
            foreach (var playerRobot in playerRobots)
            {
                if (!aggro.ProvokedPlayerRobots.Contains(playerRobot))
                    _npcFaction.IgnoreEntity((robotUid, exception), playerRobot);
            }

            foreach (var ignored in new ValueList<EntityUid>(exception.Ignored))
            {
                if (aggro.ProvokedPlayerRobots.Contains(ignored))
                    continue;

                if (!HasComp<ActorComponent>(ignored))
                    continue;

                if (TryComp<HumanoidAppearanceComponent>(ignored, out var ignoredHumanoid) && IsRobotSpecies(ignoredHumanoid))
                    continue;

                _npcFaction.UnignoreEntity((robotUid, exception), ignored);
            }
        }
    }

    private void SyncNeutralPlayerRobot(EntityUid playerRobot)
    {
        var robotQuery = EntityQueryEnumerator<MobRobotAggroComponent, FactionExceptionComponent>();
        while (robotQuery.MoveNext(out var robotUid, out var aggro, out var exception))
        {
            if (aggro.ProvokedPlayerRobots.Contains(playerRobot))
                continue;

            _npcFaction.IgnoreEntity((robotUid, exception), playerRobot);
        }
    }

    private void ProvokeAllMobRobots(EntityUid attacker)
    {
        var robotQuery = EntityQueryEnumerator<MobRobotAggroComponent, FactionExceptionComponent>();
        while (robotQuery.MoveNext(out var robotUid, out var aggro, out var exception))
        {
            aggro.ProvokedPlayerRobots.Add(attacker);
            _npcFaction.UnignoreEntity((robotUid, exception), attacker);
            _npcFaction.AggroEntity((robotUid, exception), attacker);
        }
    }

    private bool IsPlayerRobot(EntityUid uid)
    {
        return HasComp<ActorComponent>(uid)
            && TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            && IsRobotSpecies(humanoid);
    }

    private static bool IsRobotSpecies(HumanoidAppearanceComponent humanoid)
    {
        return humanoid.Species == "RobotMrHandy"
            || humanoid.Species == "RobotProtectron"
            || humanoid.Species == "RobotMrGutsy"
            || humanoid.Species == "RobotAssaultron";
    }
}
