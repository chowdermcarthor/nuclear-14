using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Spawners;

// #Misfits Add - Remove physics from spent casings on landing + enforce a global casing entity cap

namespace Content.Server._Misfits.Weapons.Guns;

/// <summary>
/// Server-side optimisation system for spent bullet casings:
///
/// <list type="number">
///   <item>Strips <see cref="PhysicsComponent"/> on landing so casings leave
///         the broadphase immediately (the original behaviour).</item>
///   <item>Enforces a global cap on concurrent casing entities. When the cap
///         is exceeded the oldest casings are deleted, preventing runaway
///         accumulation during sustained 20v20 firefights even with the
///         30-second <see cref="TimedDespawnComponent"/> timer.</item>
/// </list>
///
/// <para>
/// The no-throw-angle edge case (revolver eject, manual cycling) is now handled
/// in <c>SharedGunSystem.EjectCartridge</c> which strips physics immediately
/// when <c>angle == null</c>.
/// </para>
/// </summary>
public sealed class CasingPhysicsOptSystem : EntitySystem
{
    /// <summary>
    /// Maximum number of spent casing entities allowed to exist at once.
    /// Beyond this, the oldest are deleted. 500 is generous — a 20-player
    /// war with automatic weapons peaks around 300-600 concurrent casings
    /// at 30 s lifetime.
    /// </summary>
    private const int MaxCasings = 500;

    // FIFO queue of tracked casing UIDs for cap enforcement.
    private readonly Queue<EntityUid> _casingQueue = new();

    public override void Initialize()
    {
        base.Initialize();

        // Strip physics on landing (existing behaviour).
        SubscribeLocalEvent<CartridgeAmmoComponent, LandEvent>(OnCasingLand);

        // Track casings for the global cap when their despawn timer is attached.
        // This fires for ALL spent casings — both thrown and no-throw variants.
        SubscribeLocalEvent<CartridgeAmmoComponent, ComponentStartup>(OnCartridgeStartup);
    }

    /// <summary>
    /// Raised by <c>ThrownItemSystem</c> when a thrown entity comes to rest.
    /// Strips the physics body so the entity becomes a pure visual/timer entity.
    /// </summary>
    private void OnCasingLand(EntityUid uid, CartridgeAmmoComponent cartridge, ref LandEvent args)
    {
        if (!cartridge.Spent)
            return;

        // Deferred removal keeps us safely outside the physics engine's event stack.
        // RobustToolbox's SharedPhysicsSystem.OnPhysicsRemoved cascades fixture/broadphase
        // cleanup automatically.
        RemCompDeferred<PhysicsComponent>(uid);
    }

    /// <summary>
    /// Track spent casings for cap enforcement. We piggyback on ComponentStartup
    /// rather than adding a dedicated marker component.
    /// </summary>
    private void OnCartridgeStartup(EntityUid uid, CartridgeAmmoComponent cartridge, ComponentStartup args)
    {
        // Only track spent casings that have a despawn timer (i.e. ejected casings,
        // not cartridges sitting in a magazine).
        if (!cartridge.Spent || !HasComp<TimedDespawnComponent>(uid))
            return;

        _casingQueue.Enqueue(uid);
        TrimCasings();
    }

    /// <summary>
    /// Delete the oldest casings when the cap is exceeded.
    /// Skips already-deleted entities (natural despawn or manual cleanup).
    /// </summary>
    private void TrimCasings()
    {
        while (_casingQueue.Count > MaxCasings)
        {
            var oldest = _casingQueue.Dequeue();
            if (Exists(oldest) && !TerminatingOrDeleted(oldest))
                QueueDel(oldest);
        }
    }
}
