// #Misfits Add - Shared Nightkin passive Stealth Boy implant behavior.
using Content.Shared._Misfits.StealthBoy;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Stealth.Components;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Nightkin;

public abstract class SharedNightkinStealthSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightkinPassiveStealthComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NightkinPassiveStealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NightkinPassiveStealthComponent, ToggleNightkinStealthActionEvent>(OnToggleAction);
    }

    private void OnMapInit(Entity<NightkinPassiveStealthComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<NightkinPassiveStealthComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnToggleAction(Entity<NightkinPassiveStealthComponent> ent, ref ToggleNightkinStealthActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp<StealthBoyActiveComponent>(ent.Owner, out var active))
        {
            DeactivateNightkinStealth(ent.Owner, ent.Comp, active);
            return;
        }

        if (HasComp<StealthComponent>(ent.Owner))
        {
            if (_net.IsServer)
                _popup.PopupEntity("You are already cloaked.", ent.Owner, ent.Owner);
            return;
        }

        ActivateNightkinStealth(ent.Owner, ent.Comp);
    }

    protected abstract void ActivateNightkinStealth(EntityUid uid, NightkinPassiveStealthComponent component);

    protected abstract void DeactivateNightkinStealth(
        EntityUid uid,
        NightkinPassiveStealthComponent component,
        StealthBoyActiveComponent active);
}
