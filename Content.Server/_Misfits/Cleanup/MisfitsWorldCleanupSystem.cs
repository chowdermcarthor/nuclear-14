// #Misfits Add - Automatic despawn for puddles, footprints, and giblets to prevent entity accumulation

using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Fluids.Components;
using Content.Shared.Throwing;
using Robust.Shared.Spawners;

namespace Content.Server._Misfits.Cleanup;

/// <summary>
/// Adds <see cref="TimedDespawnComponent"/> to entity types that would otherwise
/// persist forever and silently accumulate over a long round:
/// <list type="bullet">
///   <item>Blood puddles — created every bleed tick, blood never evaporates.</item>
///   <item>Footprints — spawned every step through a puddle.</item>
///   <item>Giblets (body parts + organs) — dropped when mobs are gibbed.</item>
/// </list>
/// </summary>
public sealed class MisfitsWorldCleanupSystem : EntitySystem
{
    // Puddles (blood, chemicals, water) — 10 minutes gives plenty of time
    // for gameplay interaction (slip, forensics, mopping) before cleanup.
    private const float PuddleLifetime = 600f;

    // Body parts and organs scattered during gibbing — 10 minutes.
    private const float GibletLifetime = 600f;

    public override void Initialize()
    {
        base.Initialize();

        // Covers both regular puddles AND footprint entities (Footstep prototype
        // also carries PuddleComponent).
        SubscribeLocalEvent<PuddleComponent, ComponentStartup>(OnPuddleStartup);

        // Giblets are thrown with impulse during gibbing. When they land, we tag
        // them for cleanup. This handles the vast majority of gib scenarios.
        SubscribeLocalEvent<BodyPartComponent, LandEvent>(OnBodyPartLand);
        SubscribeLocalEvent<OrganComponent, LandEvent>(OnOrganLand);
    }

    private void OnPuddleStartup(EntityUid uid, PuddleComponent comp, ComponentStartup args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var despawn = EnsureComp<TimedDespawnComponent>(uid);

        // Only upgrade lifetime; don't shorten an existing shorter timer.
        if (despawn.Lifetime < PuddleLifetime)
            despawn.Lifetime = PuddleLifetime;
    }

    private void OnBodyPartLand(EntityUid uid, BodyPartComponent comp, ref LandEvent args)
    {
        // Part is still attached to a living body — don't despawn (e.g. held limbs).
        if (comp.Body != null)
            return;

        AddGibletDespawn(uid);
    }

    private void OnOrganLand(EntityUid uid, OrganComponent comp, ref LandEvent args)
    {
        // Organ is still inside a body — don't despawn.
        if (comp.Body != null)
            return;

        AddGibletDespawn(uid);
    }

    private void AddGibletDespawn(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var despawn = EnsureComp<TimedDespawnComponent>(uid);
        if (despawn.Lifetime < GibletLifetime)
            despawn.Lifetime = GibletLifetime;
    }
}
