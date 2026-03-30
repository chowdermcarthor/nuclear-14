// Handles the missile launch WorldTargetAction for player-controlled Sentry Bot chassis.
// When activated, the system shows a "MISSILE LOCK DETECTED" warning popup to entities
// near the targeted location, then spawns an N14ProjectileMissile projectile aimed at
// the target. Also grants the missile action on chassis initialisation.

using Content.Shared._Misfits.Robot;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Misfits.Robot;

public sealed class SentryBotMissileLauncherSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    /// <summary>
    /// Missile projectile prototype to spawn.
    /// </summary>
    private const string MissilePrototype = "N14ProjectileMissile";

    /// <summary>
    /// Range around target to show the missile lock warning popup.
    /// </summary>
    private const float WarningRange = 6f;

    /// <summary>
    /// Missile projectile speed in tiles per second.
    /// </summary>
    private const float MissileSpeed = 12f;

    public override void Initialize()
    {
        base.Initialize();

        // Grant missile launch action on chassis init.
        SubscribeLocalEvent<SentryBotChassisComponent, ComponentInit>(OnChassisInit);

        // Handle the missile launch world-target action.
        SubscribeLocalEvent<SentryBotChassisComponent, SentryBotMissileLaunchEvent>(OnMissileLaunch);
    }

    private void OnChassisInit(EntityUid uid, SentryBotChassisComponent comp, ComponentInit args)
    {
        _actions.AddAction(uid, ref comp.MissileLaunchActionEntity, "ActionSentryBotMissileLaunch");
    }

    private void OnMissileLaunch(EntityUid uid, SentryBotChassisComponent comp, SentryBotMissileLaunchEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var xform = Transform(uid);
        var fromCoords = xform.Coordinates;
        var toCoords = args.Target;

        // Show warning popup to entities near the target location.
        var targetMapPos = toCoords.ToMap(EntityManager, _transform);
        var nearbyEntities = _lookup.GetEntitiesInRange(toCoords, WarningRange);

        foreach (var nearby in nearbyEntities)
        {
            // Only warn entities that are NOT the launcher itself.
            if (nearby == uid)
                continue;

            _popup.PopupEntity(
                Loc.GetString("sentrybot-missile-lock-warning"),
                nearby,
                nearby,
                PopupType.LargeCaution);
        }

        // Spawn the missile projectile at the sentry bot's position and fire toward target.
        var fromMap = fromCoords.ToMap(EntityManager, _transform);
        var spawnCoords = _mapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? fromCoords.WithEntityId(gridUid, EntityManager)
            : new EntityCoordinates(_mapManager.GetMapEntityId(fromMap.MapId), fromMap.Position);

        var missile = Spawn(MissilePrototype, spawnCoords);
        var userVelocity = _physics.GetMapLinearVelocity(uid);
        var direction = toCoords.ToMapPos(EntityManager, _transform)
                      - spawnCoords.ToMapPos(EntityManager, _transform);

        _gun.ShootProjectile(missile, direction, userVelocity, uid, uid, MissileSpeed);
    }
}
