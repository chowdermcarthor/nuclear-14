// #Misfits Add — System backing MisfitsOreBoxMagnetComponent.
// Polls nearby ground-level entities and inserts Ore-tagged items into the box's
// storage — same logic as MagnetPickupSystem but with no inventory-slot check,
// allowing anchored structures like OreBox to benefit from the behaviour.
using Content.Server._Misfits.OreBox;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._Misfits.OreBox;

public sealed class MisfitsOreBoxMagnetSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MisfitsOreBoxMagnetComponent, StorageComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var magnet, out var storage, out var xform))
        {
            // Only operate when anchored (i.e. placed on a grid as a structure).
            if (!xform.Anchored)
                continue;

            magnet.Accumulator += frameTime;
            if (magnet.Accumulator < magnet.ScanInterval)
                continue;

            magnet.Accumulator = 0f;

            // No room — skip.
            if (!_storage.HasSpace((uid, storage)))
                continue;

            var moverCoords = _transform.GetMoverCoordinates(uid, xform);
            var finalCoords = xform.Coordinates;
            var playedSound = false;

            foreach (var near in _lookup.GetEntitiesInRange(uid, magnet.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                // Respect the storage whitelist (Ore tags).
                if (_whitelist.IsWhitelistFail(storage.Whitelist, near))
                    continue;

                // Only pick up items lying on the ground.
                if (!_physicsQuery.TryGetComponent(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                    continue;

                // Don't try to insert the box into itself.
                if (near == uid)
                    continue;

                var nearXform = Transform(near);
                var nearMap = _transform.GetMapCoordinates(near, xform: nearXform);
                var nearCoords = EntityCoordinates.FromMap(moverCoords.EntityId, nearMap, _transform, EntityManager);

                if (!_storage.Insert(uid, near, out var stacked, storageComp: storage, playSound: !playedSound))
                    continue;

                // Mirror the pickup animation from MagnetPickupSystem.
                if (stacked != null)
                    _storage.PlayPickupAnimation(stacked.Value, nearCoords, finalCoords, nearXform.LocalRotation);
                else
                    _storage.PlayPickupAnimation(near, nearCoords, finalCoords, nearXform.LocalRotation);

                playedSound = true;
            }
        }
    }
}
