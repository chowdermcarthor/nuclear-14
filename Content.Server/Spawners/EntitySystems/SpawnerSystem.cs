// Corvax-Change-Start
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.SSDIndicator;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private const float SpawnBlockRange = 15f;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TimedSpawnerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            component.TimeElapsed += frameTime;

            if (component.TimeElapsed < component.IntervalSeconds)
                continue;

            // #Misfits Change - Cap intervalsPassed to 1 to prevent dump-spawning multiple
            // cycles' worth of mobs when a player first enters an area after a long absence.
            var intervalsPassed = (int) (component.TimeElapsed / component.IntervalSeconds);
            component.TimeElapsed -= intervalsPassed * component.IntervalSeconds;
            intervalsPassed = Math.Min(intervalsPassed, 1);

            for (var i = 0; i < intervalsPassed; i++)
            {
                OnTimerFired(uid, component);
            }
        }
    }

    private void OnTimerFired(EntityUid uid, TimedSpawnerComponent component)
    {
        // #Misfits Add - Skip spawning entirely if no living player is within activation range.
        // Checked first because it's the cheapest early-out for the majority of dormant spawners.
        if (!IsPlayerNearby(uid, component))
            return;

        if (ShouldBlockSpawn(uid, component))
            return;

        // #Misfits Add - Map-wide population cap prevents unbounded NPC accumulation from roaming mobs.
        if (IsPrototypePopulationCapped(uid, component))
            return;

        if (!_random.Prob(component.Chance))
            return;

        var xform = _xformQuery.GetComponent(uid);
        var coordinates = xform.Coordinates;

        var spawnCount = _random.Next(component.MinimumEntitiesSpawned, component.MaximumEntitiesSpawned + 1);
        for (var i = 0; i < spawnCount; i++)
        {
            var entity = _random.Pick(component.Prototypes);
            SpawnAtPosition(entity, coordinates);
        }
    }

    // #Misfits Add - Proximity activation: only fire this spawner if a living, non-SSD,
    // connected humanoid player is within the configured activation range.
    private bool IsPlayerNearby(EntityUid uid, TimedSpawnerComponent component)
    {
        // 0 or negative = feature disabled, spawner always active (backwards compat).
        if (component.ActivationRange <= 0f)
            return true;

        if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.MapUid == null)
            return false;

        var mapPos = _transform.GetMapCoordinates(uid, xform: xform);

        foreach (var entity in _lookup.GetEntitiesInRange(mapPos, component.ActivationRange))
        {
            if (!Exists(entity) || entity == uid)
                continue;

            // Must be a living humanoid with an active player session, not SSD.
            if (TryComp(entity, out MobStateComponent? mob) &&
                (mob.CurrentState == MobState.Alive || mob.CurrentState == MobState.Critical) &&
                HasComp<HumanoidAppearanceComponent>(entity) &&
                HasComp<ActorComponent>(entity))
            {
                if (!TryComp<SSDIndicatorComponent>(entity, out var ssd) || !ssd.IsSSD)
                    return true;
            }
        }

        return false;
    }

    // #Misfits Add - Map-wide alive NPC population cap. Counts all alive mobs on the same
    // map whose prototype ID matches this spawner's prototype list.
    private bool IsPrototypePopulationCapped(EntityUid uid, TimedSpawnerComponent component)
    {
        if (component.MaxAlivePerPrototype <= 0)
            return false;

        if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.MapUid == null)
            return true;

        var spawnerMapUid = xform.MapUid.Value;
        var count = 0;

        var mobQuery = EntityQueryEnumerator<MobStateComponent, MetaDataComponent, TransformComponent>();
        while (mobQuery.MoveNext(out _, out var mob, out var meta, out var mobXform))
        {
            if (mob.CurrentState != MobState.Alive && mob.CurrentState != MobState.Critical)
                continue;

            if (mobXform.MapUid != spawnerMapUid)
                continue;

            if (meta.EntityPrototype?.ID is not { } protoId)
                continue;

            if (!component.Prototypes.Contains(protoId))
                continue;

            count++;

            if (count >= component.MaxAlivePerPrototype)
                return true;
        }

        return false;
    }

    private bool ShouldBlockSpawn(EntityUid uid, TimedSpawnerComponent component)
    {
        if (component.IgnoreSpawnBlock)
            return false;

        if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.MapUid == null)
            return true;

        var mapPos = _transform.GetMapCoordinates(uid, xform: xform);

        foreach (var entity in _lookup.GetEntitiesInRange(mapPos, SpawnBlockRange))
        {
            if (!Exists(entity)) continue;
            if (entity == uid) continue;

            if (TryComp(entity, out MobStateComponent? mob) &&
                (mob.CurrentState == MobState.Alive || mob.CurrentState == MobState.Critical))
            {
                if (HasComp<HumanoidAppearanceComponent>(entity))
                {
                    if (!TryComp<SSDIndicatorComponent>(entity, out var ssd) || !ssd.IsSSD)
                        return true;
                    continue;
                }

                if (TryComp(entity, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID is { } prototypeId &&
                    component.Prototypes.Contains(prototypeId))
                    return true;
            }

            else
                continue;
        }

        return false;
    }
}
// Corvax-Change-End
