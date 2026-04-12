// #Misfits Add - Shared movement speed modifier for Vault-Tec Reflex Booster.
// Applies speed buff when the active marker is present on the body entity.
// Must be shared (not server-only) for client movement prediction.

using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Shared handler for movement speed modification during reflex booster activation.
/// Runs on both client and server so movement prediction feels responsive.
/// </summary>
public sealed class SharedAugmentReflexBoosterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentReflexBoosterActiveComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(EntityUid uid, AugmentReflexBoosterActiveComponent comp,
        RefreshMovementSpeedModifiersEvent args)
    {
        // Guard: during ComponentRemove, this handler still fires.
        // Without the expiry check, the modifier gets re-applied permanently.
        if (comp.ExpiresAt != TimeSpan.Zero && comp.ExpiresAt <= _timing.CurTime)
            return;

        args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
    }
}
