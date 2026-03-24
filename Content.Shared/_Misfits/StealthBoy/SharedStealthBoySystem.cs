// #Misfits Add - Shared Stealth Boy logic. Handles activation, opacity interpolation,
// and expiry for the Fallout Stealth Boy device. Ported/inspired by RMC-14 stealth system,
// simplified to remove evasion and skill dependencies.
using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.StealthBoy;

public abstract class SharedStealthBoySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StealthBoyComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<StealthBoyComponent, ActivateStealthBoyActionEvent>(OnActivateAction);
    }

    private void OnUseInHand(Entity<StealthBoyComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Already cloaked — do nothing (single-use, consumed on activation)
        if (HasComp<StealthBoyActiveComponent>(args.User))
        {
            if (_net.IsServer)
                _popup.PopupEntity("The Stealth Boy is already active.", ent, args.User);
            return;
        }

        args.Handled = true;
        Activate(ent, args.User);
    }

    private void OnActivateAction(Entity<StealthBoyComponent> ent, ref ActivateStealthBoyActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Activate(ent, args.Performer);
    }

    protected void Activate(Entity<StealthBoyComponent> item, EntityUid user)
    {
        var now = _timing.CurTime;
        var active = EnsureComp<StealthBoyActiveComponent>(user);
        active.StartTime = now;
        active.EndTime = now + item.Comp.Duration;
        active.MinOpacity = item.Comp.MinOpacity;
        active.FadeInTime = item.Comp.FadeInTime;
        active.FadeOutTime = item.Comp.FadeOutTime;
        active.Opacity = 1f;
        active.FadingOut = false;
        Dirty(user, active);

        // Consume the item
        if (_net.IsServer)
        {
            _popup.PopupEntity("The Stealth Boy hums and you feel yourself fade from view.", user, user);
            QueueDel(item);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<StealthBoyActiveComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            var now = _timing.CurTime;

            if (!active.FadingOut)
            {
                // Fade in phase: interpolate opacity down to MinOpacity
                var fadeElapsed = (now - active.StartTime) / active.FadeInTime;
                var opacity = active.FadeInTime > TimeSpan.Zero
                    ? (float)(1.0 - (1.0 - active.MinOpacity) * Math.Min(1.0, fadeElapsed))
                    : active.MinOpacity;

                // Check if duration expired → start fade-out
                if (now >= active.EndTime)
                {
                    active.FadingOut = true;
                    active.FadeOutStart = now;
                    active.Opacity = active.MinOpacity;
                    Dirty(uid, active);
                    continue;
                }

                if (Math.Abs(opacity - active.Opacity) > 0.01f)
                {
                    active.Opacity = opacity;
                    Dirty(uid, active);
                }
            }
            else
            {
                // Fade out phase: interpolate opacity back to 1
                var fadeOutElapsed = (now - active.FadeOutStart) / active.FadeOutTime;
                var opacity = active.FadeOutTime > TimeSpan.Zero
                    ? (float)(active.MinOpacity + (1.0 - active.MinOpacity) * Math.Min(1.0, fadeOutElapsed))
                    : 1f;

                if (fadeOutElapsed >= 1.0)
                {
                    // Fade-out complete: remove component
                    RemCompDeferred<StealthBoyActiveComponent>(uid);
                    if (_net.IsServer)
                        _popup.PopupEntity("You reappear as the Stealth Boy power fades.", uid, uid);
                    continue;
                }

                if (Math.Abs(opacity - active.Opacity) > 0.01f)
                {
                    active.Opacity = opacity;
                    Dirty(uid, active);
                }
            }
        }
    }
}

/// <summary>
/// Fired when the Stealth Boy hotkey button is pressed so the item can activate from hand or worn slots.
/// </summary>
public sealed partial class ActivateStealthBoyActionEvent : InstantActionEvent;
