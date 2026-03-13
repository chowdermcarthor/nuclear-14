// #Misfits Add - Translates Perception SPECIAL stat into tighter gun spread and less recoil.
// Hooks into GunRefreshModifiersEvent (raised on the gun entity by SharedGunSystem.RefreshModifiers)
// and applies a proportional reduction to MinAngle, MaxAngle, AngleIncrease, and CameraRecoilScalar.
// Each PER point above 1 reduces these values by 0.5%, giving at most 4.5% tighter spread at PER 10.
// Intentionally tiny — all SPECIAL effects stack so individual bonuses must remain minimal.

using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Reduces gun spread and camera recoil proportionally based on the holder's
/// S.P.E.C.I.A.L. Perception stat. Subscribes globally to <see cref="GunRefreshModifiersEvent"/>
/// and walks up the gun's transform parent to find the holding player's data.
/// </summary>
public sealed class SpecialPerceptionSystem : EntitySystem
{
    // Fractional spread/recoil reduction per PER point above 1.
    // PER 10: 4.5% tighter spread and less recoil. Intentionally tiny.
    private const float PerceptionReductionPerPoint = 0.005f;

    public override void Initialize()
    {
        base.Initialize();

        // Global subscription — we receive this for every gun and check for a player holder.
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(ref GunRefreshModifiersEvent args)
    {
        // Find the entity holding this gun. Held items are directly parented
        // to the mob in the transform hierarchy (gun → mob → grid).
        var holder = Transform(args.Gun.Owner).ParentUid;

        if (!TryComp<PersistentPlayerDataComponent>(holder, out var data))
            return;

        var per = data.Perception;
        if (per <= 1)
            return;

        // Each PER point above 1 contributes a 0.5% multiplicative reduction.
        // At PER 10: 4.5% reduction to all spread/recoil values.
        var reduction = (per - 1) * PerceptionReductionPerPoint;
        var keepFraction = 1.0 - reduction;

        // Reduce cone angles — smaller angles = tighter grouping.
        args.MinAngle    = new Angle((double) args.MinAngle    * keepFraction);
        args.MaxAngle    = new Angle((double) args.MaxAngle    * keepFraction);
        // AngleIncrease drives bloom per shot; reducing it means less spread build-up.
        args.AngleIncrease = new Angle((double) args.AngleIncrease * keepFraction);
        // Camera recoil scalar controls screenshake magnitude.
        args.CameraRecoilScalar *= (float) keepFraction;
    }
}
