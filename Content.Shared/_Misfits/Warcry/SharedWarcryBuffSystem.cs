using Content.Shared.Movement.Systems;
using Robust.Shared.Log;

namespace Content.Shared._Misfits.Warcry;

/// <summary>
/// Shared system that applies warcry speed modifiers on both client and server,
/// ensuring client-side movement prediction correctly accounts for the buff.
/// </summary>
public sealed class SharedWarcryBuffSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    private static readonly ISawmill Log = Logger.GetSawmill("warcry.buff");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarcryBuffComponent, ComponentStartup>(OnBuffStartup);
        SubscribeLocalEvent<WarcryBuffComponent, ComponentRemove>(OnBuffRemove);
        SubscribeLocalEvent<WarcryBuffComponent, RefreshMovementSpeedModifiersEvent>(OnBuffRefreshSpeed);
    }

    private void OnBuffStartup(EntityUid uid, WarcryBuffComponent component, ComponentStartup args)
    {
        Log.Info($"WarcryBuff STARTUP on {uid}: SpeedBonus={component.SpeedBonus}");
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnBuffRemove(EntityUid uid, WarcryBuffComponent component, ComponentRemove args)
    {
        Log.Info($"WarcryBuff REMOVED on {uid}: SpeedBonus={component.SpeedBonus}");
        if (TerminatingOrDeleted(uid))
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnBuffRefreshSpeed(EntityUid uid, WarcryBuffComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var speedModifier = 1f + component.SpeedBonus;
        Log.Debug($"WarcryBuff REFRESH on {uid}: bonus={component.SpeedBonus} → modifier={speedModifier}");
        args.ModifySpeed(speedModifier, speedModifier);
    }
}
