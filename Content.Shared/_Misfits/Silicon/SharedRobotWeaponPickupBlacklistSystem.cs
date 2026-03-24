using Content.Shared.Item;
using Content.Shared.Whitelist;

namespace Content.Shared._Misfits.Silicon;

/// <summary>
/// Keeps player robot hands usable for utility items while refusing floor weapons.
/// </summary>
public sealed class SharedRobotWeaponPickupBlacklistSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RobotWeaponPickupBlacklistComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, RobotWeaponPickupBlacklistComponent component, PickupAttemptEvent args)
    {
        if (_whitelist.IsBlacklistPass(component.Blacklist, args.Item))
            args.Cancel();
    }
}