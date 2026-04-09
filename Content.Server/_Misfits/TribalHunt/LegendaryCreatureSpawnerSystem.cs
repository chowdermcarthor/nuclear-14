using Content.Shared._Misfits.TribalHunt;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Handles spawning, tracking, and loot drops for legendary creatures during tribal hunts.
/// </summary>
public sealed partial class LegendaryCreatureSpawnerSystem : EntitySystem
{
    private const int SpawnAttempts = 30;
    private const float MinSpawnDistance = 100f;
    private const float MaxSpawnDistance = 500f;
    private const int LegendaryHealthMultiplier = 3;

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryCreatureComponent, DestructionEventArgs>(OnCreatureDestroyed);
    }

    /// <summary>
    /// Spawns a legendary creature near the hunt leader on a valid, unobstructed tile.
    /// </summary>
    public EntityUid? TrySpawnLegendaryCreature(string creatureProto, EntityUid huntSessionId, MapCoordinates leaderMapCoords)
    {
        EntityCoordinates spawnCoords = default;
        var foundSpawn = false;

        for (var i = 0; i < SpawnAttempts; i++)
        {
            var offset = _random.NextVector2(MinSpawnDistance, MaxSpawnDistance);
            var candidate = new MapCoordinates(leaderMapCoords.Position + offset, leaderMapCoords.MapId);

            if (!TryGetValidSpawnCoordinates(candidate, out spawnCoords))
                continue;

            foundSpawn = true;
            break;
        }

        if (!foundSpawn)
            return null;

        var creature = Spawn(creatureProto, spawnCoords);
        ApplyLegendaryHealthMultiplier(creature);

        var legComp = EnsureComp<LegendaryCreatureComponent>(creature);
        legComp.HuntSessionId = huntSessionId;
        legComp.CreatureName = "Deathclaw";
        legComp.LeatherDropCount = 3;
        legComp.RevealLocation = true;
        Dirty(creature, legComp);

        return creature;
    }

    private void ApplyLegendaryHealthMultiplier(EntityUid creature)
    {
        if (!TryComp(creature, out MobThresholdsComponent? thresholds))
            return;

        ScaleThreshold(creature, MobState.SoftCritical, thresholds);
        ScaleThreshold(creature, MobState.Critical, thresholds);
        ScaleThreshold(creature, MobState.Dead, thresholds);
    }

    private void ScaleThreshold(EntityUid creature, MobState state, MobThresholdsComponent thresholds)
    {
        var threshold = _mobThreshold.GetThresholdForState(creature, state, thresholds);
        if (threshold <= 0)
            return;

        _mobThreshold.SetMobStateThreshold(creature, threshold * LegendaryHealthMultiplier, state, thresholds);
    }

    private bool TryGetValidSpawnCoordinates(MapCoordinates mapCoords, out EntityCoordinates spawnCoords)
    {
        spawnCoords = default;

        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var gridComp))
            return false;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, gridComp, mapCoords);
        var gridCoords = _mapSystem.GridTileToLocal(gridUid, gridComp, tileIndices);
        var tileRef = _mapSystem.GetTileRef(gridUid, gridComp, gridCoords);

        if (tileRef.Tile.IsSpace() || _turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
            return false;

        spawnCoords = gridCoords;
        return true;
    }

    private void OnCreatureDestroyed(EntityUid uid, LegendaryCreatureComponent comp, DestructionEventArgs args)
    {
        RaiseLocalEvent(uid, new LegendaryCreatureKilledEvent());

        for (int i = 0; i < comp.LeatherDropCount; i++)
        {
            SpawnNextToOrDrop("TribalLegendaryLeather", uid);
        }
    }
}
